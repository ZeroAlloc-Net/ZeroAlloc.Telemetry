using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZeroAlloc.Telemetry.Generator.Models;

namespace ZeroAlloc.Telemetry.Generator;

[Generator]
public sealed class InstrumentGenerator : IIncrementalGenerator
{
    private const string InstrumentAttributeFqn    = "ZeroAlloc.Telemetry.InstrumentAttribute";
    private const string TraceAttributeFqn         = "ZeroAlloc.Telemetry.TraceAttribute";
    private const string CountAttributeFqn         = "ZeroAlloc.Telemetry.CountAttribute";
    private const string HistogramAttributeFqn     = "ZeroAlloc.Telemetry.HistogramAttribute";
    private const string TraceTagAttributeFqn      = "ZeroAlloc.Telemetry.TraceTagAttribute";
    private const string TraceTagFromResultAttrFqn = "ZeroAlloc.Telemetry.TraceTagFromResultAttribute";
    private const string TraceTagConstantAttrFqn   = "ZeroAlloc.Telemetry.TraceTagConstantAttribute";
    private const string CountFromResultAttrFqn     = "ZeroAlloc.Telemetry.CountFromResultAttribute";
    private const string HistogramFromResultAttrFqn = "ZeroAlloc.Telemetry.HistogramFromResultAttribute";
    private const string MetricTagFromResultAttrFqn = "ZeroAlloc.Telemetry.MetricTagFromResultAttribute";

    /// <summary>
    /// Fully-qualified names that keep nullable reference type annotations.
    /// </summary>
    /// <remarks>
    /// <see cref="SymbolDisplayFormat.FullyQualifiedFormat"/> omits the <c>?</c> suffix. Since the
    /// proxy implements the interface, dropping it does not merely lose information — the
    /// signatures stop matching and the consumer's build fails with CS8613 on a return type or
    /// CS8767 on a parameter (Telemetry#29). The annotation is part of the signature, so it has to
    /// survive the round-trip through the model.
    /// </remarks>
    private static readonly SymbolDisplayFormat TypeFormat =
        SymbolDisplayFormat.FullyQualifiedFormat.AddMiscellaneousOptions(
            SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    /// <summary>Names of the pipeline steps, so tests can read their run reasons.</summary>
    internal static class TrackingNames
    {
        public const string Instruments = "Instruments";
        public const string OrphanDiagnostics = "OrphanDiagnostics";
        public const string ProxyCollisions = "ProxyCollisions";
    }

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        // Broaden the predicate to every TypeDeclarationSyntax so we can raise
        // ZTEL001 on class/struct/record misuse (the ForAttributeWithMetadataName
        // filter was previously interface-only and silently dropped invalid targets).
        var results = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                InstrumentAttributeFqn,
                predicate: static (node, _) => node is TypeDeclarationSyntax,
                transform: static (ctx, _) => Parse(ctx))
            .Where(static r => r is not null)
            .Select(static (r, _) => r!.Value)
            .WithTrackingName(TrackingNames.Instruments);

        // ZTEL015 and ZTEL016 - the one check that needs every proxy at once. Only the hint names
        // that must not be added reach the per-interface outputs, so an edit that leaves them
        // equal keeps every other proxy's output cached.
        var collisions = results
            .Select(static (r, _) => r.File)
            .Where(static f => f is not null)
            .Select(static (f, _) => f!)
            .Collect()
            .Select(static (files, _) => ProxyCollisions.Find(files))
            .WithTrackingName(TrackingNames.ProxyCollisions);

        context.RegisterSourceOutput(collisions, static (ctx, found) =>
        {
            foreach (var diag in found.Diagnostics)
                ctx.ReportDiagnostic(diag.ToDiagnostic());
        });

        // Report diagnostics collected during parse, then emit code only when
        // the target is valid and its proxy collides with no earlier one.
        context.RegisterSourceOutput(results.Combine(collisions), static (ctx, pair) =>
        {
            var (result, found) = pair;
            foreach (var diag in result.Diagnostics)
                ctx.ReportDiagnostic(diag.ToDiagnostic());

            if (result.Model is { } model && !found.Skips(model.HintName))
            {
                ctx.AddSource(model.HintName, ProxyWriter.Write(model));
            }
        });

        // ZTEL003: method-level metric, trace and tag attributes on a method whose containing type
        // lacks [Instrument] are silently ignored. Scan every method carrying any of them and
        // check the enclosing type.
        RegisterMethodAttributeDiagnostic(context, TraceAttributeFqn, "Trace");
        RegisterMethodAttributeDiagnostic(context, CountAttributeFqn, "Count");
        RegisterMethodAttributeDiagnostic(context, HistogramAttributeFqn, "Histogram");
        RegisterMethodAttributeDiagnostic(context, CountFromResultAttrFqn, "CountFromResult");
        RegisterMethodAttributeDiagnostic(context, HistogramFromResultAttrFqn, "HistogramFromResult");
        RegisterMethodAttributeDiagnostic(context, MetricTagFromResultAttrFqn, "MetricTagFromResult");
        RegisterMethodAttributeDiagnostic(context, TraceTagFromResultAttrFqn, "TraceTagFromResult");
        RegisterMethodAttributeDiagnostic(context, TraceTagConstantAttrFqn, "TraceTagConstant");
    }

    private static void RegisterMethodAttributeDiagnostic(
        IncrementalGeneratorInitializationContext context,
        string methodAttrFqn,
        string shortName)
    {
        var orphans = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                methodAttrFqn,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: (ctx, _) =>
                {
                    if (ctx.TargetSymbol is not IMethodSymbol method) return (DiagnosticInfo?)null;
                    var containing = method.ContainingType;
                    if (containing is null) return null;
                    foreach (var a in containing.GetAttributes())
                    {
                        if (string.Equals(a.AttributeClass?.ToDisplayString(), InstrumentAttributeFqn, StringComparison.Ordinal))
                            return null; // Container has [Instrument] — proxy is generated.
                    }
                    var loc = method.Locations.FirstOrDefault() ?? Location.None;
                    return DiagnosticInfo.Create(
                        InstrumentDiagnostics.MethodAttributeWithoutInstrument,
                        loc,
                        shortName, containing.Name, method.Name);
                })
            .Where(static d => d is not null)
            .Select(static (d, _) => d!)
            .WithTrackingName(TrackingNames.OrphanDiagnostics);

        context.RegisterSourceOutput(orphans, static (ctx, diag) => ctx.ReportDiagnostic(diag.ToDiagnostic()));
    }

    private static ParseResult? Parse(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol target) return null;

        var instrumentAttr = ctx.Attributes[0];
        var attrLocation = instrumentAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
            ?? target.Locations.FirstOrDefault()
            ?? Location.None;

        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();

        // ZTEL001: the generator emits a proxy CLASS implementing the target.
        // That only makes sense when the target is an interface.
        if (target.TypeKind != TypeKind.Interface)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.InstrumentOnNonInterface,
                attrLocation,
                target.ToDisplayString()));
            return new ParseResult(null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()), null);
        }

        // ZTEL014, ZTEL017 and ZTEL013: the proxy is emitted in a file of its own, next to the
        // interface, so it can only be generated when that file can see and reopen what it needs.
        if (Ungeneratable(target) is { } blocked)
        {
            diagnostics.Add(blocked);
            return new ParseResult(null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()), null);
        }

        // ActivitySource is the first positional constructor argument.
        var activitySource = instrumentAttr.ConstructorArguments.Length > 0
            ? instrumentAttr.ConstructorArguments[0].Value as string ?? string.Empty
            : string.Empty;

        // ZTEL002: empty ActivitySource leaves subscribers with nothing to match against.
        if (string.IsNullOrWhiteSpace(activitySource))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.EmptyActivitySource,
                attrLocation,
                target.ToDisplayString()));
            return new ParseResult(null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()), null);
        }

        // PublicProxy is a named argument; absent means the default (internal proxy).
        var publicProxy = false;
        foreach (var named in instrumentAttr.NamedArguments)
        {
            if (string.Equals(named.Key, "PublicProxy", StringComparison.Ordinal))
                publicProxy = named.Value.Value is true;
        }

        var methods = BuildMethods(target, ctx.SemanticModel.Compilation, diagnostics);
        var model = BuildModel(target, activitySource, methods, publicProxy);

        return new ParseResult(
            model,
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()),
            ProxyFileFor(target, model));
    }

    private static InstrumentModel BuildModel(
        INamedTypeSymbol target, string activitySource, EquatableArray<MethodModel> methods, bool publicProxy)
    {
        var ns        = target.ContainingNamespace.IsGlobalNamespace ? null : target.ContainingNamespace.ToDisplayString();
        var ifaceName = target.Name;
        var proxyName = (ifaceName.StartsWith("I", StringComparison.Ordinal) && ifaceName.Length > 1)
                        ? ifaceName.Substring(1) + "Instrumented"
                        : ifaceName + "Instrumented";

        // A generic interface gets a generic proxy, and a nested one a proxy next to it.
        var typeParameters = TypeDeclarations.TypeParameterList(target);
        return new InstrumentModel(
            HintNames.ForInterface(target),
            ns,
            TypeDeclarations.Identifier(ifaceName) + typeParameters,
            proxyName,
            activitySource,
            methods,
            publicProxy,
            typeParameters,
            TypeDeclarations.ConstraintClauses(target, TypeFormat),
            TypeDeclarations.ContainingDeclarations(target));
    }

    /// <summary>
    /// The diagnostic that stops a proxy from being generated at all, or null. Reported on the
    /// interface's name.
    /// </summary>
    private static DiagnosticInfo? Ungeneratable(INamedTypeSymbol target)
    {
        var location = target.Locations.FirstOrDefault(static l => l.IsInSource);

        if (TypeDeclarations.FileLocalType(target) is { } fileLocal)
        {
            return DiagnosticInfo.Create(
                InstrumentDiagnostics.FileLocal, location, target.ToDisplayString(), fileLocal.ToDisplayString());
        }

        if (TypeDeclarations.VariantContainingInterface(target) is { } variant)
        {
            return DiagnosticInfo.Create(
                InstrumentDiagnostics.VariantContainingInterface, location, target.ToDisplayString(), variant.ToDisplayString());
        }

        if (TypeDeclarations.FirstNonPartialContainingType(target) is { } notPartial)
        {
            return DiagnosticInfo.Create(
                InstrumentDiagnostics.ContainingTypeNotPartial, location, target.ToDisplayString(), notPartial.ToDisplayString());
        }

        return null;
    }

    /// <summary>
    /// The proxy as the collision check sees it: its file name, and its name next to the
    /// interface, as in <c>App.Outer+FooInstrumented`1</c>.
    /// </summary>
    private static ProxyFile? ProxyFileFor(INamedTypeSymbol target, InstrumentModel model)
    {
        var proxyName = model.ProxyName;
        var typeParameters = model.TypeParameters;
        if (LocationInfo.From(target.Locations.FirstOrDefault(static l => l.IsInSource)) is not { } location)
            return null;

        var arity = target.Arity > 0
            ? "`" + target.Arity.ToString(System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;
        string key;
        string display;
        if (target.ContainingType is { } containing)
        {
            key = MetadataName(containing) + "+" + proxyName + arity;
            display = containing.ToDisplayString() + "." + proxyName + typeParameters;
        }
        else
        {
            var ns = target.ContainingNamespace.IsGlobalNamespace
                ? string.Empty
                : target.ContainingNamespace.ToDisplayString() + ".";
            key = ns + proxyName + arity;
            display = ns + proxyName + typeParameters;
        }

        return new ProxyFile(model.HintName, key, display, target.ToDisplayString(), location);
    }

    private static string MetadataName(INamedTypeSymbol type)
    {
        if (type.ContainingType is { } outer) return MetadataName(outer) + "+" + type.MetadataName;
        var ns = type.ContainingNamespace.IsGlobalNamespace
            ? string.Empty
            : type.ContainingNamespace.ToDisplayString() + ".";
        return ns + type.MetadataName;
    }

    private static EquatableArray<MethodModel> BuildMethods(
        INamedTypeSymbol target,
        Compilation compilation,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var methods = ImmutableArray.CreateBuilder<MethodModel>();
        foreach (var member in target.GetMembers().OfType<IMethodSymbol>())
        {
            var traceName   = GetAttributeFirstArg(member, TraceAttributeFqn);

            var returnType  = member.ReturnType.ToDisplayString(TypeFormat);
            var isAsync     = TaskShapes.IsAwaitable(member.ReturnType);
            var returnsVoid = member.ReturnsVoid || TaskShapes.IsVoidAwaitable(member.ReturnType);

            var parameters = BuildParameters(compilation, member, traceName is null, diagnostics);
            var resultType = TaskShapes.UnwrapAwaited(member.ReturnType);
            var resultTags = BuildResultTags(compilation, member, resultType, returnsVoid, traceName is null, diagnostics);
            var constantTags = BuildConstantTags(member);

            var count         = BuildPlainMetric(compilation, target, member, CountAttributeFqn, "Count", MetricKind.Counter, resultType, returnsVoid, diagnostics);
            var histogram     = BuildPlainMetric(compilation, target, member, HistogramAttributeFqn, "Histogram", MetricKind.Histogram, resultType, returnsVoid, diagnostics);
            var resultMetrics = BuildResultMetrics(compilation, target, member, resultType, returnsVoid, diagnostics);
            var metricTags    = BuildMetricTags(compilation, target, member, resultType, returnsVoid, diagnostics);

            ReportTagDiagnostics(diagnostics, target, member, parameters, resultTags, constantTags, traceName, returnsVoid);
            ReportUnknownSpanNameTokens(diagnostics, target, member, traceName);

            methods.Add(new MethodModel(
                member.Name,
                returnType,
                isAsync,
                returnsVoid,
                ToEquatable(parameters),
                traceName,
                count,
                histogram,
                ToEquatable(resultTags),
                ResultCanBeNull(member),
                ToEquatable(constantTags),
                ToEquatable(resultMetrics),
                ToEquatable(metricTags),
                BuildTraceNameExpression(traceName)));
        }
        return new EquatableArray<MethodModel>(methods.ToImmutable());
    }

    /// <summary>The only token recognised inside a <c>[Trace]</c> span name.</summary>
    private const string ImplTypeToken = "{type}";

    /// <summary>
    /// Builds the C# expression that composes a span name containing <c>{type}</c> from the
    /// wrapped instance's type name, or null when the name is a plain constant.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>[Trace]</c> lives on the interface method, so without this every implementation of a
    /// multi-implementation interface produces the same span name — a vector store's Qdrant and
    /// Weaviate backends become indistinguishable in a trace, which is usually the distinction
    /// the span existed to draw.
    /// </para>
    /// <para>
    /// The result is emitted against a local named <c>_implName</c> that the proxy constructor
    /// establishes. Composing there rather than at the call site means the concatenation happens
    /// once per wrapped instance instead of once per call, so instrumenting a hot path still
    /// allocates nothing per invocation.
    /// </para>
    /// </remarks>
    private static string? BuildTraceNameExpression(string? traceName)
    {
        if (traceName is null || traceName.IndexOf(ImplTypeToken, StringComparison.Ordinal) < 0)
            return null;

        var parts = traceName.Split(new[] { ImplTypeToken }, StringSplitOptions.None);
        var sb = new StringBuilder();

        for (var i = 0; i < parts.Length; i++)
        {
            // Every part after the first is preceded by an occurrence of the token.
            if (i > 0)
            {
                if (sb.Length > 0) sb.Append(" + ");
                sb.Append("_implName");
            }

            // Skip empty literals so "{type}" yields `_implName`, not `"" + _implName + ""`.
            if (parts[i].Length == 0) continue;

            if (sb.Length > 0) sb.Append(" + ");
            sb.Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(parts[i], quote: true));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Reports any <c>{...}</c> in a span name that is not <c>{type}</c>. Left undiagnosed, a
    /// typo such as <c>{Type}</c> is not an error — it is emitted verbatim, and the first sign of
    /// trouble is a literal brace sitting in a dashboard weeks later.
    /// </summary>
    private static void ReportUnknownSpanNameTokens(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        INamedTypeSymbol target,
        IMethodSymbol member,
        string? traceName)
    {
        if (traceName is null) return;

        var i = 0;
        while (i < traceName.Length)
        {
            var open = traceName.IndexOf('{', i);
            if (open < 0) break;

            var close = traceName.IndexOf('}', open + 1);
            if (close < 0) break;

            var token = traceName.Substring(open, close - open + 1);
            if (!string.Equals(token, ImplTypeToken, StringComparison.Ordinal))
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    InstrumentDiagnostics.UnknownSpanNameToken,
                    member.Locations.FirstOrDefault() ?? target.Locations.FirstOrDefault(),
                    token,
                    target.Name,
                    member.Name));
            }

            i = close + 1;
        }
    }

    /// <summary>
    /// Whether the value a result tag reads from can be null, after unwrapping the awaited type
    /// of <c>Task&lt;T&gt;</c>, <c>ValueTask&lt;T&gt;</c> or a task-like type. Emitting <c>?.</c> against a non-nullable
    /// value type is a compile error, so the writer needs to know which operator to use.
    /// </summary>
    private static bool ResultCanBeNull(IMethodSymbol method) =>
        PathResolver.CanBeNull(TaskShapes.UnwrapAwaited(method.ReturnType));

    /// <summary>
    /// A tag with nowhere to go is a silent no-op, which is worse than a build message: the
    /// telemetry simply never appears and the gap is only noticed downstream.
    /// </summary>
    private static void ReportTagDiagnostics(
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        INamedTypeSymbol target,
        IMethodSymbol member,
        IReadOnlyList<ParameterModel> parameters,
        IReadOnlyList<ResultTagModel> resultTags,
        IReadOnlyList<ConstantTagModel> constantTags,
        string? traceName,
        bool returnsVoid)
    {
        var location = member.Locations.FirstOrDefault() ?? Location.None;

        if (traceName is null)
        {
            var hasParamTag = parameters.Any(p => p.TagName is not null);
            if (hasParamTag)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    InstrumentDiagnostics.TagWithoutTrace,
                    location, "TraceTag", target.ToDisplayString(), member.Name));
            }

            if (resultTags.Count > 0)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    InstrumentDiagnostics.TagWithoutTrace,
                    location, "TraceTagFromResult", target.ToDisplayString(), member.Name));
            }

            if (constantTags.Count > 0)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    InstrumentDiagnostics.TagWithoutTrace,
                    location, "TraceTagConstant", target.ToDisplayString(), member.Name));
            }
        }

        // Reported independently of [Trace]: the attribute is wrong on a void method either way.
        if (returnsVoid && resultTags.Count > 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.ResultReadOnVoidMethod,
                location, "TraceTagFromResult", target.ToDisplayString(), member.Name));
        }
    }

    /// <summary>
    /// Builds the parameter models. A <c>[TraceTag]</c> whose member path does not resolve is
    /// reported as ZTEL010 and dropped, so no tag is emitted for it.
    /// </summary>
    /// <remarks>
    /// Before ZTEL010 an unresolved path tagged the whole argument under a name that promised one
    /// member of it, so the tag was wrong rather than missing and nothing said so. Without
    /// <c>[Trace]</c> no tag is emitted at all and ZTEL004 already reports it, so the path is not
    /// resolved there: a second warning would only repeat the first.
    /// </remarks>
    private static ParameterModel[] BuildParameters(
        Compilation compilation,
        IMethodSymbol method,
        bool untraced,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var ps = method.Parameters;
        var result = new ParameterModel[ps.Length];
        for (var i = 0; i < ps.Length; i++)
        {
            var (tagName, member, attr) = GetTraceTag(ps[i]);

            string? accessSuffix = null;
            var needsCopy = false;

            if (tagName is not null && !untraced && !string.IsNullOrEmpty(member))
            {
                var path = PathResolver.Resolve(compilation, ps[i].Type, member!);
                if (path.Resolved)
                {
                    accessSuffix = path.Access;

                    // A copy is only needed when the emitted access actually null-tests the
                    // argument; a plain `.Member` on a non-nullable value leaves its state alone.
                    needsCopy = path.Access!.StartsWith("?.", StringComparison.Ordinal);
                }
                else
                {
                    var fallback = ps[i].Locations.FirstOrDefault() ?? MethodLocation(method);
                    diagnostics.Add(ParameterTagPathNotFound(
                        AttributeLocations.Positional(attr!, 1, "member", fallback), member!, ps[i].Name, path));
                    tagName = null;
                }
            }

            result[i] = new ParameterModel(
                ps[i].Type.ToDisplayString(TypeFormat),
                ps[i].Name,
                tagName,
                accessSuffix,
                needsCopy);
        }

        return result;
    }

    private static (string? Name, string? Member, AttributeData? Attribute) GetTraceTag(IParameterSymbol parameter)
    {
        foreach (var attr in parameter.GetAttributes())
        {
            if (!string.Equals(attr.AttributeClass?.ToDisplayString(), TraceTagAttributeFqn, StringComparison.Ordinal))
                continue;

            if (attr.ConstructorArguments.Length == 0)
                continue;

            var name = attr.ConstructorArguments[0].Value as string;

            // Second positional argument is the optional member path.
            var member = attr.ConstructorArguments.Length > 1
                ? attr.ConstructorArguments[1].Value as string
                : null;

            return (name, member, attr);
        }

        return (null, null, null);
    }

    private static ResultTagModel[] BuildResultTags(
        Compilation compilation,
        IMethodSymbol method,
        ITypeSymbol resultType,
        bool returnsVoid,
        bool untraced,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        List<ResultTagModel>? tags = null;
        foreach (var attr in method.GetAttributes())
        {
            if (!IsAttribute(attr, TraceTagFromResultAttrFqn))
                continue;

            if (attr.ConstructorArguments.Length == 0)
                continue;

            var name = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrEmpty(name))
                continue;

            // Second positional argument is the optional member path.
            var member = attr.ConstructorArguments.Length > 1
                ? attr.ConstructorArguments[1].Value as string
                : null;

            // The writer emits result tags only with a span and a result, so neither case reads
            // the path. With no result, ZTEL005 reports the attribute, and resolving against Task
            // would only add a misleading ZTEL007. Without [Trace], ZTEL004 reports it, and the
            // method compiled before path validation existed, so a bad path must not now fail it.
            // The unresolved tag is kept so that those warnings still fire.
            if (returnsVoid || untraced)
            {
                (tags ??= new List<ResultTagModel>()).Add(new ResultTagModel(name!, member));
                continue;
            }

            string? accessSuffix = null;
            var pathOk = true;
            if (!string.IsNullOrEmpty(member))
            {
                var path = PathResolver.Resolve(compilation, resultType, member!);
                if (path.Resolved)
                {
                    accessSuffix = path.Access;
                }
                else
                {
                    diagnostics.Add(PathNotFound(
                        AttributeLocations.Positional(attr, 1, "member", MethodLocation(method)), member!, path));
                    pathOk = false;
                }
            }

            // Checked even after a bad path, so one build reports every error on the attribute.
            var guardOk = TryBuildGuard(
                compilation, attr, method, resultType, GetNamedString(attr, "When"), diagnostics, out var guard);

            if (!pathOk || !guardOk)
                continue;

            (tags ??= new List<ResultTagModel>()).Add(new ResultTagModel(name!, member, accessSuffix, guard));
        }

        return tags?.ToArray() ?? [];
    }

    /// <summary>
    /// Renders an attribute constant as the C# literal to emit, or null if it cannot be.
    /// </summary>
    /// <remarks>
    /// Strings and chars go through Roslyn's <c>SymbolDisplay.FormatLiteral</c> so quoting and
    /// escaping match the language — a tag value containing a quote or a backslash would
    /// otherwise emit code that does not compile.
    /// <para>
    /// Enums are emitted as a cast over the underlying value rather than by member name. The name
    /// would read better, but a cast is correct for combined flag values too, which have no single
    /// member to name.
    /// </para>
    /// </remarks>
    private static string? FormatConstant(TypedConstant constant)
    {
        if (constant.IsNull)
            return "null";

        switch (constant.Kind)
        {
            case TypedConstantKind.Primitive when constant.Value is string s:
                return Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(s, quote: true);

            case TypedConstantKind.Primitive when constant.Value is char c:
                return Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(c, quote: true);

            case TypedConstantKind.Primitive when constant.Value is bool b:
                return b ? "true" : "false";

            case TypedConstantKind.Primitive:
                return System.Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture);

            case TypedConstantKind.Enum when constant.Type is not null:
                var enumType = constant.Type.ToDisplayString(TypeFormat);
                var underlying = System.Convert.ToString(constant.Value, System.Globalization.CultureInfo.InvariantCulture);
                return $"({enumType}){underlying}";

            default:
                // Arrays and typeof() have no sensible tag representation; skip rather than
                // emit something that will not compile.
                return null;
        }
    }

    /// <summary>
    /// Resolves a <c>When</c> guard into the condition appended to the result root. Returns false,
    /// having reported ZTEL007 or ZTEL008, when the guard cannot be emitted. The caller then emits
    /// nothing for the attribute, so the diagnostic is the only error the user sees.
    /// </summary>
    /// <remarks>
    /// The comparison against true is added only when the guard can be null, either because a
    /// step along the path is null-tested or because the member is <c>bool?</c>. On a plain bool
    /// the bare expression is emitted, since <c>x == true</c> reads as noise.
    /// </remarks>
    private static bool TryBuildGuard(
        Compilation compilation,
        AttributeData attr,
        IMethodSymbol method,
        ITypeSymbol resultType,
        string? when,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics,
        out string? guard)
    {
        guard = null;
        if (string.IsNullOrWhiteSpace(when))
            return true;

        var location = AttributeLocations.Named(attr, "When", MethodLocation(method));
        var path = PathResolver.Resolve(compilation, resultType, when!);
        if (!path.Resolved)
        {
            diagnostics.Add(PathNotFound(location, when!, path));
            return false;
        }

        // A guard through dynamic cannot be checked, but compiled on 1.6.4: it is evaluated at
        // run time, and the comparison below makes a null or false value skip the read.
        if (!PathResolver.IsBoolean(path.FinalType!) && !PathResolver.IsDynamic(path.FinalType!))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.WhenNotBoolean, location, when, path.FinalType!.ToDisplayString()));
            return false;
        }

        guard = path.CanBeNull ? path.Access + " == true" : path.Access;
        return true;
    }

    private static DiagnosticInfo PathNotFound(Location location, string path, PathResolver.Resolution resolution) =>
        string.IsNullOrEmpty(resolution.MissingSegment)
            ? DiagnosticInfo.Create(InstrumentDiagnostics.MemberPathEmptySegment, location, path)
            : DiagnosticInfo.Create(
                InstrumentDiagnostics.MemberPathNotFound,
                location,
                resolution.MissingSegment,
                path,
                resolution.MissingOn?.ToDisplayString());

    private static DiagnosticInfo ParameterTagPathNotFound(
        Location location, string path, string parameterName, PathResolver.Resolution resolution) =>
        string.IsNullOrEmpty(resolution.MissingSegment)
            ? DiagnosticInfo.Create(InstrumentDiagnostics.ParameterTagPathEmptySegment, location, path, parameterName)
            : DiagnosticInfo.Create(
                InstrumentDiagnostics.ParameterTagPathNotFound,
                location,
                resolution.MissingSegment,
                path,
                resolution.MissingOn?.ToDisplayString(),
                parameterName);

    private static bool IsAttribute(AttributeData attr, string attributeFqn) =>
        string.Equals(attr.AttributeClass?.ToDisplayString(), attributeFqn, StringComparison.Ordinal);

    private static string? GetNamedString(AttributeData attr, string name)
    {
        foreach (var named in attr.NamedArguments)
        {
            if (string.Equals(named.Key, name, StringComparison.Ordinal))
                return named.Value.Value as string;
        }

        return null;
    }

    private static Location MethodLocation(IMethodSymbol method) =>
        method.Locations.FirstOrDefault() ?? Location.None;

    private static ConstantTagModel[] BuildConstantTags(IMethodSymbol method)
    {
        List<ConstantTagModel>? tags = null;
        foreach (var attr in method.GetAttributes())
        {
            if (!string.Equals(attr.AttributeClass?.ToDisplayString(), TraceTagConstantAttrFqn, StringComparison.Ordinal))
                continue;

            if (attr.ConstructorArguments.Length < 2)
                continue;

            var name = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrEmpty(name))
                continue;

            var literal = FormatConstant(attr.ConstructorArguments[1]);
            if (literal is null)
                continue;

            (tags ??= new List<ConstantTagModel>()).Add(new ConstantTagModel(name!, literal));
        }

        return tags?.ToArray() ?? [];
    }

    private static string? GetAttributeFirstArg(IMethodSymbol method, string attributeFqn)
    {
        foreach (var attr in method.GetAttributes())
        {
            if (!string.Equals(attr.AttributeClass?.ToDisplayString(), attributeFqn, StringComparison.Ordinal))
                continue;

            if (attr.ConstructorArguments.Length > 0)
                return attr.ConstructorArguments[0].Value as string;
        }

        return null;
    }

    /// <summary>
    /// Builds the model for <c>[Count]</c> or <c>[Histogram]</c>. Null when the method has none, or
    /// when its <c>When</c> guard cannot be emitted.
    /// </summary>
    private static MetricModel? BuildPlainMetric(
        Compilation compilation,
        INamedTypeSymbol target,
        IMethodSymbol method,
        string attributeFqn,
        string shortName,
        MetricKind kind,
        ITypeSymbol resultType,
        bool returnsVoid,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var attr = FindAttribute(method, attributeFqn);
        if (attr is null || attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Value is not string metric)
            return null;

        var when = GetNamedString(attr, "When");
        string? guard = null;
        if (!string.IsNullOrWhiteSpace(when))
        {
            // A guard needs a result to read. Recording unguarded instead would count exactly the
            // calls the guard was written to exclude, so the instrument records nothing.
            if (returnsVoid)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    InstrumentDiagnostics.ResultReadOnVoidMethod,
                    MethodLocation(method),
                    $"{shortName}(When = {Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(when!, quote: true)})",
                    target.ToDisplayString(),
                    method.Name));
                return null;
            }

            if (!TryBuildGuard(compilation, attr, method, resultType, when, diagnostics, out guard))
                return null;
        }

        return new MetricModel(kind, metric, GetNamedString(attr, "Unit"), GetNamedString(attr, "Description"), guard);
    }

    private const string CounterTypesReason =
        "A counter adds long values, so the member must be sbyte, byte, short, ushort, int, uint or long, or a nullable form of one";

    private const string HistogramTypesReason =
        "A histogram records double values, so the member must be sbyte, byte, short, ushort, int, uint, long, ulong, float, double or decimal, or a nullable form of one";

    private const string DynamicTypeReason =
        "The type of a dynamic member is only known at run time, so it cannot be checked against the instrument; expose a typed member instead";

    /// <summary>The last sentence of ZTEL009: what the instrument accepts, or why dynamic cannot be checked.</summary>
    private static string FitReason(MetricKind kind, ITypeSymbol type) =>
        PathResolver.IsDynamic(type) ? DynamicTypeReason
        : kind == MetricKind.Counter ? CounterTypesReason
        : HistogramTypesReason;

    /// <summary>
    /// Builds <c>[CountFromResult]</c> and <c>[HistogramFromResult]</c> in attribute order. One that
    /// cannot be emitted is reported and left out, so its diagnostic is the only error.
    /// </summary>
    /// <remarks>
    /// Unlike result tags, these need no <c>[Trace]</c>: they are metrics, not span data, so they
    /// are always resolved and validated.
    /// </remarks>
    private static MetricModel[] BuildResultMetrics(
        Compilation compilation,
        INamedTypeSymbol target,
        IMethodSymbol method,
        ITypeSymbol resultType,
        bool returnsVoid,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        List<MetricModel>? metrics = null;
        foreach (var attr in method.GetAttributes())
        {
            MetricKind kind;
            string shortName;
            if (IsAttribute(attr, CountFromResultAttrFqn))
            {
                kind = MetricKind.Counter;
                shortName = "CountFromResult";
            }
            else if (IsAttribute(attr, HistogramFromResultAttrFqn))
            {
                kind = MetricKind.Histogram;
                shortName = "HistogramFromResult";
            }
            else
            {
                continue;
            }

            if (attr.ConstructorArguments.Length < 2 || attr.ConstructorArguments[0].Value is not string metric)
                continue;

            // No result, so nothing to resolve: reading the path against Task would only add a
            // misleading ZTEL007 to the ZTEL005 that already explains the problem.
            if (returnsVoid)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    InstrumentDiagnostics.ResultReadOnVoidMethod,
                    MethodLocation(method), shortName, target.ToDisplayString(), method.Name));
                continue;
            }

            if (BuildResultMetric(compilation, attr, method, kind, shortName, metric, resultType, diagnostics) is { } model)
                (metrics ??= new List<MetricModel>()).Add(model);
        }

        return metrics?.ToArray() ?? [];
    }

    /// <summary>
    /// Resolves and type-checks one result-driven instrument. Null, having reported ZTEL007,
    /// ZTEL008 or ZTEL009, when it cannot be emitted.
    /// </summary>
    private static MetricModel? BuildResultMetric(
        Compilation compilation,
        AttributeData attr,
        IMethodSymbol method,
        MetricKind kind,
        string shortName,
        string metric,
        ITypeSymbol resultType,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var member = attr.ConstructorArguments[1].Value as string ?? string.Empty;
        var memberLocation = AttributeLocations.Positional(attr, 1, "member", MethodLocation(method));

        var path = PathResolver.Resolve(compilation, resultType, member);
        var valueOk = path.Resolved;
        if (!valueOk)
        {
            diagnostics.Add(PathNotFound(memberLocation, member, path));
        }
        else if (!Fits(kind, path.FinalType!))
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.MemberDoesNotFitInstrument,
                memberLocation,
                shortName,
                member.Length == 0 ? "the return value" : $"'{member}'",
                path.FinalType!.ToDisplayString(),
                FitReason(kind, path.FinalType!)));
            valueOk = false;
        }

        // Checked even after a bad member, so one build reports every error on the attribute.
        var guardOk = TryBuildGuard(
            compilation, attr, method, resultType, GetNamedString(attr, "When"), diagnostics, out var guard);

        if (!valueOk || !guardOk)
            return null;

        return new MetricModel(
            kind,
            metric,
            GetNamedString(attr, "Unit"),
            GetNamedString(attr, "Description"),
            guard,
            path.Access,
            path.CanBeNull,
            PathResolver.UnwrapNullable(path.FinalType!).SpecialType == SpecialType.System_Decimal);
    }

    /// <summary>
    /// Whether the value can go to the instrument without a cast the user did not write, or with
    /// the explicit decimal cast the spec calls for.
    /// </summary>
    /// <remarks>
    /// Explicit SpecialType sets rather than <c>Compilation.ClassifyConversion</c>. That would also
    /// admit <c>char</c>, and any type with a user-defined implicit conversion, and neither is a
    /// quantity a counter or a histogram should silently accept.
    /// </remarks>
    private static bool Fits(MetricKind kind, ITypeSymbol type)
    {
        var special = PathResolver.UnwrapNullable(type).SpecialType;

        var convertsToLong = special is SpecialType.System_SByte or SpecialType.System_Byte
            or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64;

        if (kind == MetricKind.Counter)
            return convertsToLong;

        return convertsToLong
            || special is SpecialType.System_UInt64 or SpecialType.System_Single
                or SpecialType.System_Double or SpecialType.System_Decimal;
    }

    /// <summary>
    /// The metric names the method declares, in the order the writer emits them: <c>[Count]</c>,
    /// <c>[Histogram]</c>, then the result metrics in attribute order. Declared, not emitted: a
    /// metric dropped for its own error still counts, so the tag does not add a second diagnostic
    /// pointing away from the real one.
    /// </summary>
    private static string[] DeclaredMetrics(IMethodSymbol method)
    {
        var names = new List<string>();
        foreach (var fqn in new[] { CountAttributeFqn, HistogramAttributeFqn })
        {
            if (FindAttribute(method, fqn) is { ConstructorArguments.Length: > 0 } attr
                && attr.ConstructorArguments[0].Value is string name)
            {
                names.Add(name);
            }
        }

        foreach (var attr in method.GetAttributes())
        {
            if ((IsAttribute(attr, CountFromResultAttrFqn) || IsAttribute(attr, HistogramFromResultAttrFqn))
                && attr.ConstructorArguments.Length > 0
                && attr.ConstructorArguments[0].Value is string name)
            {
                names.Add(name);
            }
        }

        return names.ToArray();
    }

    /// <summary>
    /// Builds <c>[MetricTagFromResult]</c> in attribute order. One that cannot be emitted, or that
    /// no declared metric would carry, is reported and left out.
    /// </summary>
    /// <remarks>
    /// The path and guard are resolved even for a tag reported as ZTEL011 or ZTEL012, so one build
    /// reports every error on the attribute.
    /// </remarks>
    private static MetricTagModel[] BuildMetricTags(
        Compilation compilation,
        INamedTypeSymbol target,
        IMethodSymbol method,
        ITypeSymbol resultType,
        bool returnsVoid,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        List<MetricTagModel>? tags = null;
        string[]? declared = null;
        HashSet<(string Metric, string Tag)>? taken = null;

        foreach (var attr in method.GetAttributes())
        {
            if (!IsAttribute(attr, MetricTagFromResultAttrFqn))
                continue;

            if (attr.ConstructorArguments.Length < 2 || attr.ConstructorArguments[0].Value is not string name || name.Length == 0)
                continue;

            // No result, so nothing to resolve: see BuildResultMetrics.
            if (returnsVoid)
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    InstrumentDiagnostics.ResultReadOnVoidMethod,
                    MethodLocation(method), "MetricTagFromResult", target.ToDisplayString(), method.Name));
                continue;
            }

            declared ??= DeclaredMetrics(method);
            var filter = GetNamedString(attr, "Metric");
            var applies = ApplicableMetrics(attr, target, method, name, filter, declared, diagnostics);
            var unique = IsUniqueOnItsMetrics(attr, target, method, name, applies, taken ??= new(), diagnostics);

            var member = attr.ConstructorArguments[1].Value as string ?? string.Empty;
            var path = PathResolver.Resolve(compilation, resultType, member);
            if (!path.Resolved)
            {
                diagnostics.Add(PathNotFound(
                    AttributeLocations.Positional(attr, 1, "member", MethodLocation(method)), member, path));
            }

            var guardOk = TryBuildGuard(
                compilation, attr, method, resultType, GetNamedString(attr, "When"), diagnostics, out var guard);

            if (applies.Length == 0 || !unique || !path.Resolved || !guardOk)
                continue;

            (tags ??= new List<MetricTagModel>()).Add(
                new MetricTagModel(name, filter, path.Access!, path.CanBeNull, guard));
        }

        return tags?.ToArray() ?? [];
    }

    /// <summary>
    /// The declared metrics the tag goes on. Empty, having reported ZTEL011, when the method
    /// declares none or <paramref name="filter"/> names none of them.
    /// </summary>
    private static string[] ApplicableMetrics(
        AttributeData attr,
        INamedTypeSymbol target,
        IMethodSymbol method,
        string name,
        string? filter,
        string[] declared,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var tagText = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(name, quote: true);

        if (declared.Length == 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.MetricTagWithoutMetric,
                AttributeLocations.Positional(attr, 0, "name", MethodLocation(method)),
                tagText, target.ToDisplayString(), method.Name));
            return [];
        }

        if (filter is null)
            return declared;

        if (declared.Contains(filter, StringComparer.Ordinal))
            return [filter];

        diagnostics.Add(DiagnosticInfo.Create(
            InstrumentDiagnostics.MetricTagUnknownMetric,
            AttributeLocations.Named(attr, "Metric", MethodLocation(method)),
            tagText, target.ToDisplayString(), method.Name, filter));
        return [];
    }

    /// <summary>
    /// Claims the tag name on each of <paramref name="metrics"/>. False, having reported ZTEL012
    /// for the first metric already carrying the name, when an earlier tag holds one of them.
    /// </summary>
    private static bool IsUniqueOnItsMetrics(
        AttributeData attr,
        INamedTypeSymbol target,
        IMethodSymbol method,
        string name,
        string[] metrics,
        HashSet<(string Metric, string Tag)> taken,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var metric in metrics)
        {
            if (!taken.Contains((metric, name)))
                continue;

            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.DuplicateMetricTag,
                AttributeLocations.Positional(attr, 0, "name", MethodLocation(method)),
                name, metric, target.ToDisplayString(), method.Name));
            return false;
        }

        foreach (var metric in metrics)
            taken.Add((metric, name));

        return true;
    }

    private static EquatableArray<T> ToEquatable<T>(T[] items)
        where T : IEquatable<T> =>
        new(ImmutableArray.Create(items));

    private static AttributeData? FindAttribute(IMethodSymbol method, string attributeFqn)
    {
        foreach (var attr in method.GetAttributes())
        {
            if (IsAttribute(attr, attributeFqn))
                return attr;
        }

        return null;
    }

    /// <param name="File">The proxy as the collision check sees it; null when none is generated.</param>
    private readonly record struct ParseResult(InstrumentModel? Model, EquatableArray<DiagnosticInfo> Diagnostics, ProxyFile? File);
}
