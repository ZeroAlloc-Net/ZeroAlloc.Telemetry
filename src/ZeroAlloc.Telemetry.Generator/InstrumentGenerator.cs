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
    private const string MetricTagAttrFqn           = "ZeroAlloc.Telemetry.MetricTagAttribute";
    private const string MetricTagConstantAttrFqn   = "ZeroAlloc.Telemetry.MetricTagConstantAttribute";

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

        // The assembly's informational version, the default version of every source and meter. A
        // string, so an edit that leaves it alone keeps every proxy's output cached.
        var assemblyVersion = context.CompilationProvider
            .Select(static (compilation, _) => InformationalVersion(compilation));

        // Report diagnostics collected during parse, then emit code only when
        // the target is valid and its proxy collides with no earlier one.
        context.RegisterSourceOutput(results.Combine(collisions).Combine(assemblyVersion), static (ctx, pair) =>
        {
            var ((result, found), version) = pair;
            foreach (var diag in result.Diagnostics)
                ctx.ReportDiagnostic(diag.ToDiagnostic());

            if (result.Model is { } model && !found.Skips(model.HintName))
            {
                ctx.AddSource(model.HintName, ProxyWriter.Write(model, model.Version ?? version));
            }
        });

        // ZTEL003: method-level metric, trace and tag attributes on a method whose containing type
        // lacks [Instrument] are silently ignored. Scan every method carrying any of them and
        // check the enclosing type. An interface extended by an instrumented one is not an
        // orphan: the proxy implements its members, with their attributes.
        var instrumentedBases = results
            .Select(static (r, _) => r.Bases)
            .Collect()
            .Select(static (all, _) => new EquatableArray<string>(
                all.SelectMany(static b => b).Distinct(StringComparer.Ordinal).OrderBy(static b => b, StringComparer.Ordinal).ToImmutableArray()));
        RegisterOrphanDiagnostics(context, instrumentedBases);
    }

    private static void RegisterOrphanDiagnostics(
        IncrementalGeneratorInitializationContext context, IncrementalValueProvider<EquatableArray<string>> bases)
    {
        RegisterMethodAttributeDiagnostic(context, bases, TraceAttributeFqn, "Trace");
        RegisterMethodAttributeDiagnostic(context, bases, CountAttributeFqn, "Count");
        RegisterMethodAttributeDiagnostic(context, bases, HistogramAttributeFqn, "Histogram");
        RegisterMethodAttributeDiagnostic(context, bases, CountFromResultAttrFqn, "CountFromResult");
        RegisterMethodAttributeDiagnostic(context, bases, HistogramFromResultAttrFqn, "HistogramFromResult");
        RegisterMethodAttributeDiagnostic(context, bases, MetricTagFromResultAttrFqn, "MetricTagFromResult");
        RegisterMethodAttributeDiagnostic(context, bases, MetricTagConstantAttrFqn, "MetricTagConstant");
        RegisterMethodAttributeDiagnostic(context, bases, TraceTagFromResultAttrFqn, "TraceTagFromResult");
        RegisterMethodAttributeDiagnostic(context, bases, TraceTagConstantAttrFqn, "TraceTagConstant");
    }

    /// <summary>A ZTEL003 candidate and the type it is on, which an instrumented interface may extend.</summary>
    private readonly record struct Orphan(DiagnosticInfo Diagnostic, string ContainingType);

    /// <summary>
    /// The compilation's <c>AssemblyInformationalVersionAttribute</c> value, or null when it has
    /// none. The .NET SDK sets it from the project's <c>Version</c>.
    /// </summary>
    private static string? InformationalVersion(Compilation compilation)
    {
        foreach (var attr in compilation.Assembly.GetAttributes())
        {
            if (string.Equals(attr.AttributeClass?.ToDisplayString(), "System.Reflection.AssemblyInformationalVersionAttribute", StringComparison.Ordinal)
                && attr.ConstructorArguments.Length == 1
                && attr.ConstructorArguments[0].Value is string { Length: > 0 } version)
            {
                return version;
            }
        }

        return null;
    }

    private static void RegisterMethodAttributeDiagnostic(
        IncrementalGeneratorInitializationContext context,
        IncrementalValueProvider<EquatableArray<string>> bases,
        string methodAttrFqn,
        string shortName)
    {
        var orphans = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                methodAttrFqn,
                predicate: static (node, _) => node is MethodDeclarationSyntax,
                transform: (ctx, _) =>
                {
                    if (ctx.TargetSymbol is not IMethodSymbol method) return (Orphan?)null;
                    var containing = method.ContainingType;
                    if (containing is null) return null;
                    foreach (var a in containing.GetAttributes())
                    {
                        if (string.Equals(a.AttributeClass?.ToDisplayString(), InstrumentAttributeFqn, StringComparison.Ordinal))
                            return null; // Container has [Instrument] — proxy is generated.
                    }
                    var loc = method.Locations.FirstOrDefault() ?? Location.None;
                    return new Orphan(
                        DiagnosticInfo.Create(
                            InstrumentDiagnostics.MethodAttributeWithoutInstrument,
                            loc,
                            shortName, containing.Name, method.Name),
                        containing.OriginalDefinition.ToDisplayString());
                })
            .Where(static d => d is not null)
            .Select(static (d, _) => d!.Value)
            .WithTrackingName(TrackingNames.OrphanDiagnostics);

        context.RegisterSourceOutput(orphans.Combine(bases), static (ctx, pair) =>
        {
            var (orphan, instrumentedBases) = pair;
            if (!instrumentedBases.Contains(orphan.ContainingType, StringComparer.Ordinal))
                ctx.ReportDiagnostic(orphan.Diagnostic.ToDiagnostic());
        });
    }

    /// <summary>The result for a target that gets no proxy: only its diagnostics.</summary>
    private static ParseResult Rejected(ImmutableArray<DiagnosticInfo>.Builder diagnostics) =>
        new(null, new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()), null);

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
            return Rejected(diagnostics);
        }

        // ZTEL014, ZTEL017 and ZTEL013: the proxy is emitted in a file of its own, next to the
        // interface, so it can only be generated when that file can see and reopen what it needs.
        if (Ungeneratable(target) is { } blocked)
        {
            diagnostics.Add(blocked);
            return Rejected(diagnostics);
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
            return Rejected(diagnostics);
        }

        // Named arguments; absent means the default: an internal proxy, the assembly's version.
        var methods = BuildMethods(target, ctx.SemanticModel.Compilation, diagnostics);
        var model = BuildModel(target, activitySource, methods, GetNamedBool(instrumentAttr, "PublicProxy")) with
        {
            Version = GetNamedString(instrumentAttr, "Version"),
            Properties = ProxyMembers.BuildProperties(target, TypeFormat, diagnostics),
        };

        return new ParseResult(
            model,
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()),
            ProxyFileFor(target, model),
            new EquatableArray<string>(target.AllInterfaces.Select(static i => i.OriginalDefinition.ToDisplayString()).ToImmutableArray()));
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

        // ZTEL025: a static abstract member has to be implemented as a static member, which
        // cannot reach the wrapped instance. Reported on the member.
        if (ProxyMembers.FirstStaticAbstract(target) is { } staticAbstract)
        {
            return DiagnosticInfo.Create(
                InstrumentDiagnostics.StaticAbstractMember,
                staticAbstract.Locations.FirstOrDefault(static l => l.IsInSource) ?? location,
                target.ToDisplayString(),
                staticAbstract.ToDisplayString());
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
        foreach (var member in ProxyMembers.Methods(target))
        {
            if (member.ReturnsByRef || member.ReturnsByRefReadonly)
            {
                methods.Add(ProxyMembers.RefReturning(target, member, TypeFormat, diagnostics));
                continue;
            }

            var traceName   = GetAttributeFirstArg(member, TraceAttributeFqn);

            var returnType  = member.ReturnType.ToDisplayString(TypeFormat);
            var isAsync     = TaskShapes.IsAwaitable(member.ReturnType);
            var configurable = isAsync && TaskShapes.HasConfigureAwait(member.ReturnType);
            var returnsVoid = member.ReturnsVoid || TaskShapes.IsVoidAwaitable(member.ReturnType);

            var parameters = BuildParameters(compilation, member, traceName is null, diagnostics);
            var resultType = TaskShapes.UnwrapAwaited(member.ReturnType);
            var resultTags = BuildResultTags(compilation, member, resultType, returnsVoid, traceName is null, diagnostics);
            var constantTags = BuildConstantTags(target, member, diagnostics);

            var count         = BuildPlainMetric(compilation, target, member, CountAttributeFqn, "Count", MetricKind.Counter, resultType, returnsVoid, diagnostics);
            var histogram     = BuildPlainMetric(compilation, target, member, HistogramAttributeFqn, "Histogram", MetricKind.Histogram, resultType, returnsVoid, diagnostics);
            var resultMetrics = BuildResultMetrics(compilation, target, member, resultType, returnsVoid, diagnostics);
            var metricTags    = BuildMetricTags(compilation, target, member, resultType, returnsVoid, diagnostics);

            ReportTagDiagnostics(diagnostics, target, member, parameters, resultTags, constantTags, traceName, returnsVoid);
            var spanName = ParseSpanName(compilation, target, member, traceName, diagnostics);
            var trace = BuildTraceOptions(compilation, target, member, resultType, returnsVoid, spanName, diagnostics);

            methods.Add(new MethodModel(
                member.Name,
                returnType,
                isAsync,
                returnsVoid,
                ToEquatable(parameters),
                spanName.StartName,
                count,
                histogram,
                ToEquatable(resultTags),
                ResultCanBeNull(member),
                ToEquatable(constantTags),
                ToEquatable(resultMetrics),
                ToEquatable(metricTags),
                BuildTraceNameExpression(spanName.StartName),
                TypeDeclarations.TypeParameterList(member.TypeParameters),
                TypeDeclarations.ConstraintClauses(member.TypeParameters, TypeFormat),
                trace,
                configurable,
                Receiver: ProxyMembers.Receiver(target, member, TypeFormat)));
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
    /// A <c>[Trace]</c> name split into the name the span starts under and, when it has parameter
    /// tokens, the expression composing its full display name.
    /// </summary>
    private readonly record struct SpanName(string? StartName, string? DisplayName, EquatableArray<string> Copies);

    /// <summary>
    /// Parses the tokens of a <c>[Trace]</c> name. <c>{type}</c> stays in the start name, which
    /// the constructor resolves. <c>{parameter}</c> and <c>{parameter.Member}</c> are left out of
    /// the start name and go into a display name, set only on a sampled span, so an unsampled call
    /// builds no string. Anything else is reported as ZTEL006 and kept verbatim.
    /// </summary>
    /// <remarks>
    /// The start name is what is left with the parameter tokens removed, its whitespace collapsed,
    /// or the method name when nothing is left: <c>"{operation} {model}"</c> starts as the method
    /// name and is displayed as <c>"chat gpt-9"</c>. A name with no parameter token is returned
    /// unchanged, so its span is emitted exactly as before.
    /// </remarks>
    private static SpanName ParseSpanName(
        Compilation compilation,
        INamedTypeSymbol target,
        IMethodSymbol member,
        string? traceName,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (traceName is null)
            return new SpanName(null, null, default);

        var start = new StringBuilder();
        var display = new StringBuilder();
        var copies = ImmutableArray.CreateBuilder<string>();
        var hasParameterToken = false;
        var i = 0;
        while (i < traceName.Length)
        {
            var open = traceName.IndexOf('{', i);
            var close = open < 0 ? -1 : traceName.IndexOf('}', open + 1);
            if (close < 0)
            {
                AppendLiteral(start, display, traceName.Substring(i));
                break;
            }

            AppendLiteral(start, display, traceName.Substring(i, open - i));
            var token = traceName.Substring(open, close - open + 1);
            i = close + 1;

            if (string.Equals(token, ImplTypeToken, StringComparison.Ordinal))
            {
                start.Append(token);
                display.Append("{_inner.GetType().Name}");
                continue;
            }

            if (AppendParameterToken(compilation, target, member, token, display, copies, diagnostics))
            {
                hasParameterToken = true;
            }
            else
            {
                diagnostics.Add(DiagnosticInfo.Create(
                    InstrumentDiagnostics.UnknownSpanNameToken,
                    member.Locations.FirstOrDefault() ?? target.Locations.FirstOrDefault(),
                    token, target.Name, member.Name));
                AppendLiteral(start, display, token);
            }
        }

        if (!hasParameterToken)
            return new SpanName(traceName, null, default);

        var startName = string.Join(" ", start.ToString().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return new SpanName(
            startName.Length == 0 ? member.Name : startName,
            "string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $\"" + display + "\")",
            new EquatableArray<string>(copies.ToImmutable()));
    }

    /// <summary>
    /// Literal text goes into the start name as is, and into the display name as a string literal
    /// in a hole, which needs no escaping of braces or quotes inside the interpolated string.
    /// </summary>
    private static void AppendLiteral(StringBuilder start, StringBuilder display, string text)
    {
        if (text.Length == 0)
            return;

        start.Append(text);
        display.Append('{').Append(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(text, quote: true)).Append('}');
    }

    /// <summary>
    /// Appends a <c>{parameter}</c> or <c>{parameter.Member}</c> token to the display name. False
    /// when the token names no parameter. A path that does not resolve is reported as ZTEL022 and
    /// left out, but is still a parameter token, so it is not kept in the start name either.
    /// </summary>
    private static bool AppendParameterToken(
        Compilation compilation,
        INamedTypeSymbol target,
        IMethodSymbol member,
        string token,
        StringBuilder display,
        ImmutableArray<string>.Builder copies,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var content = token.Substring(1, token.Length - 2);
        var dot = content.IndexOf('.');
        var parameter = FindParameter(member, dot < 0 ? content : content.Substring(0, dot));
        if (parameter is null)
            return false;

        var path = PathResolver.Resolve(compilation, parameter.Type, dot < 0 ? string.Empty : content.Substring(dot + 1));
        var unreadable = ProxyMembers.UnreadableAtStart(parameter)
            ?? (path.Resolved && path.FinalType!.IsRefLikeType ? ProxyMembers.RefStructValue : null);
        if (unreadable is not null)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.UnreadableParameter,
                member.Locations.FirstOrDefault() ?? target.Locations.FirstOrDefault(),
                "Trace", parameter.Name, target.Name, member.Name, unreadable));
        }
        else if (path.Resolved)
        {
            AppendParameterValue(display, copies, parameter, path.Access!);
        }
        else
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.SpanNameTokenPathNotFound,
                member.Locations.FirstOrDefault() ?? target.Locations.FirstOrDefault(),
                path.MissingSegment, token, target.Name, member.Name, path.MissingOn?.ToDisplayString()));
        }

        return true;
    }

    /// <summary>
    /// A parameter token's hole in the display name. The argument is read through a copy when the
    /// access null-tests it, as <c>[TraceTag]</c> does: it is forwarded to the inner call, and
    /// testing it would leave it maybe-null there.
    /// </summary>
    private static void AppendParameterValue(
        StringBuilder display, ImmutableArray<string>.Builder copies, IParameterSymbol parameter, string access)
    {
        var root = TypeDeclarations.Identifier(parameter.Name);
        if (access.StartsWith("?.", StringComparison.Ordinal))
        {
            var copy = "_nameArg" + copies.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
            copies.Add($"var {copy} = {root};");
            root = copy;
        }

        display.Append('{').Append(root).Append(access).Append('}');
    }

    private static IParameterSymbol? FindParameter(IMethodSymbol method, string name)
    {
        foreach (var parameter in method.Parameters)
        {
            if (string.Equals(parameter.Name, name, StringComparison.Ordinal))
                return parameter;
        }

        return null;
    }

    /// <summary>
    /// Reads what <c>[Trace]</c> asks for beyond a plain span: its kind, the error status from the
    /// result, the display name and the tags at start. Null when it asks for none of them.
    /// </summary>
    private static TraceOptions? BuildTraceOptions(
        Compilation compilation,
        INamedTypeSymbol target,
        IMethodSymbol method,
        ITypeSymbol resultType,
        bool returnsVoid,
        SpanName spanName,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (FindAttribute(method, TraceAttributeFqn) is not { } attr)
            return null;

        var kind = TraceKind(attr);
        var tagsAtStart = GetNamedBool(attr, "TagsAtStart");
        var errorWhen = GetNamedString(attr, "ErrorWhen");
        var errorDescription = GetNamedString(attr, "ErrorDescription");

        string? guard = null;
        string? description = null;
        if (!string.IsNullOrWhiteSpace(errorWhen) && returnsVoid)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.ResultReadOnVoidMethod,
                MethodLocation(method),
                $"Trace(ErrorWhen = {Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(errorWhen!, quote: true)})",
                target.ToDisplayString(),
                method.Name));
        }
        else if (!string.IsNullOrWhiteSpace(errorWhen)
            && TryBuildGuard(compilation, attr, method, resultType, errorWhen, diagnostics, out guard, "ErrorWhen"))
        {
            description = ErrorDescriptionAccess(compilation, attr, method, resultType, errorDescription, diagnostics);
        }

        if (kind is null && guard is null && spanName.DisplayName is null && !tagsAtStart)
            return null;

        return new TraceOptions(kind, guard, description, spanName.DisplayName, spanName.Copies, tagsAtStart);
    }

    /// <summary>The span kind as an expression, or null for the default <c>Internal</c>.</summary>
    private static string? TraceKind(AttributeData attr)
    {
        foreach (var named in attr.NamedArguments)
        {
            if (!string.Equals(named.Key, "Kind", StringComparison.Ordinal) || named.Value.Value is not int value || value == 0)
                continue;

            foreach (var field in named.Value.Type!.GetMembers().OfType<IFieldSymbol>())
            {
                if (field.HasConstantValue && field.ConstantValue is int constant && constant == value)
                    return "ActivityKind." + field.Name;
            }

            return "(ActivityKind)" + value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        return null;
    }

    /// <summary>
    /// The <c>ErrorDescription</c> access, converted to a string, or null when there is none or it
    /// does not resolve, having reported ZTEL007.
    /// </summary>
    private static string? ErrorDescriptionAccess(
        Compilation compilation,
        AttributeData attr,
        IMethodSymbol method,
        ITypeSymbol resultType,
        string? errorDescription,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (string.IsNullOrWhiteSpace(errorDescription))
            return null;

        var path = PathResolver.Resolve(compilation, resultType, errorDescription!);
        if (!path.Resolved)
        {
            diagnostics.Add(PathNotFound(
                AttributeLocations.Named(attr, "ErrorDescription", MethodLocation(method)), errorDescription!, path));
            return null;
        }

        if (path.FinalType!.SpecialType == SpecialType.System_String)
            return path.Access;

        return path.Access + (PathResolver.CanBeNull(path.FinalType) ? "?.ToString()" : ".ToString()");
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
            var tag = tagName is null || untraced
                ? default
                : ResolveParameterTag(compilation, method, ps[i], member, attr!, diagnostics);
            if (tag.Dropped)
                tagName = null;

            var accessSuffix = tag.AccessSuffix;
            var needsCopy = tag.NeedsCopy;
            var canBeNull = tag.Resolved ? tag.CanBeNull : PathResolver.CanBeNull(ps[i].Type);

            result[i] = new ParameterModel(
                ps[i].Type.ToDisplayString(TypeFormat),
                ps[i].Name,
                tagName,
                accessSuffix,
                needsCopy,
                canBeNull,
                ProxyMembers.Modifier(ps[i]),
                ProxyMembers.ArgumentModifier(ps[i]),
                ps[i].RefKind == RefKind.Out,
                ps[i].Type.IsRefLikeType);
        }

        return result;
    }

    /// <summary>How a <c>[TraceTag]</c> reads its parameter, once resolved.</summary>
    private readonly record struct ParameterTag(bool Dropped, bool Resolved, string? AccessSuffix, bool NeedsCopy, bool CanBeNull);

    /// <summary>
    /// Resolves a <c>[TraceTag]</c> member path. The tag is dropped, having been reported, when the
    /// path does not resolve, ZTEL010, or when the value cannot be read at the span's start: an out
    /// parameter, or a ref struct value, ZTEL024.
    /// </summary>
    private static ParameterTag ResolveParameterTag(
        Compilation compilation,
        IMethodSymbol method,
        IParameterSymbol parameter,
        string? member,
        AttributeData attr,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var attrLocation = attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? MethodLocation(method);
        var unreadable = ProxyMembers.UnreadableAtStart(parameter)
            ?? (string.IsNullOrEmpty(member) && parameter.Type.IsRefLikeType ? ProxyMembers.RefStructValue : null);

        PathResolver.Resolution? path = null;
        if (unreadable is null && !string.IsNullOrEmpty(member))
        {
            path = PathResolver.Resolve(compilation, parameter.Type, member!);
            if (path.Resolved && path.FinalType!.IsRefLikeType)
            {
                unreadable = ProxyMembers.RefStructValue;
                attrLocation = AttributeLocations.Positional(attr, 1, "member", MethodLocation(method));
            }
        }

        if (unreadable is not null)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.UnreadableParameter,
                attrLocation, "TraceTag", parameter.Name, method.ContainingType.Name, method.Name, unreadable));
            return new ParameterTag(true, false, null, false, false);
        }

        if (path is null)
            return default;

        if (!path.Resolved)
        {
            var fallback = parameter.Locations.FirstOrDefault() ?? MethodLocation(method);
            diagnostics.Add(ParameterTagPathNotFound(
                AttributeLocations.Positional(attr, 1, "member", fallback), member!, parameter.Name, path, "TraceTag"));
            return new ParameterTag(true, false, null, false, false);
        }

        // A copy is only needed when the emitted access actually null-tests the argument; a plain
        // `.Member` on a non-nullable value leaves its state alone.
        return new ParameterTag(false, true, path.Access, path.Access!.StartsWith("?.", StringComparison.Ordinal), path.CanBeNull);
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
        out string? guard,
        string propertyName = "When")
    {
        guard = null;
        if (string.IsNullOrWhiteSpace(when))
            return true;

        var location = AttributeLocations.Named(attr, propertyName, MethodLocation(method));
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
                InstrumentDiagnostics.WhenNotBoolean, location, when, path.FinalType!.ToDisplayString(), propertyName));
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
        Location location, string path, string parameterName, PathResolver.Resolution resolution, string attributeName) =>
        string.IsNullOrEmpty(resolution.MissingSegment)
            ? DiagnosticInfo.Create(InstrumentDiagnostics.ParameterTagPathEmptySegment, location, path, parameterName, attributeName)
            : DiagnosticInfo.Create(
                InstrumentDiagnostics.ParameterTagPathNotFound,
                location,
                resolution.MissingSegment,
                path,
                resolution.MissingOn?.ToDisplayString(),
                parameterName,
                attributeName);

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

    private static ConstantTagModel[] BuildConstantTags(
        INamedTypeSymbol target, IMethodSymbol method, ImmutableArray<DiagnosticInfo>.Builder diagnostics)
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
            {
                diagnostics.Add(UnsupportedConstant(attr, target, method, "TraceTagConstant", name!));
                continue;
            }

            (tags ??= new List<ConstantTagModel>()).Add(new ConstantTagModel(name!, literal));
        }

        return tags?.ToArray() ?? [];
    }

    /// <summary>ZTEL021: a constant tag whose value no tag can carry.</summary>
    private static DiagnosticInfo UnsupportedConstant(
        AttributeData attr, INamedTypeSymbol target, IMethodSymbol method, string shortName, string name) =>
        DiagnosticInfo.Create(
            InstrumentDiagnostics.UnsupportedConstantTagValue,
            AttributeLocations.Positional(attr, 1, "value", MethodLocation(method)),
            shortName, name, target.ToDisplayString(), method.Name);

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

        var unit = GetNamedString(attr, "Unit");
        if (kind == MetricKind.Counter)
            return new MetricModel(kind, metric, unit, GetNamedString(attr, "Description"), guard);

        return new MetricModel(
            kind,
            metric,
            unit,
            GetNamedString(attr, "Description"),
            guard,
            ElapsedMember: ElapsedMember(attr, method, metric, unit, diagnostics),
            Buckets: BuildBuckets(compilation, attr, method, shortName, metric, diagnostics));
    }

    /// <summary>
    /// The <c>TimeSpan</c> property a <c>[Histogram]</c> records, chosen by its unit so the value
    /// is in the unit the instrument declares. Before 1.9 every duration was milliseconds, so a
    /// histogram declared in seconds recorded values a thousand times too large.
    /// </summary>
    private static string ElapsedMember(
        AttributeData attr, IMethodSymbol method, string metric, string? unit, ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        switch (unit)
        {
            case null:
            case "ms":
                return "TotalMilliseconds";
            case "s":
                return "TotalSeconds";
            case "us":
                return "TotalMicroseconds";
            case "ns":
                return "TotalNanoseconds";
            case "min":
                return "TotalMinutes";
            case "h":
                return "TotalHours";
            default:
                diagnostics.Add(DiagnosticInfo.Create(
                    InstrumentDiagnostics.UnconvertibleHistogramUnit,
                    AttributeLocations.Named(attr, "Unit", MethodLocation(method)),
                    metric, unit));
                return "TotalMilliseconds";
        }
    }

    /// <summary>
    /// The <c>Buckets</c> of a histogram attribute as C# literals, or empty when there are none or
    /// they cannot be emitted, having reported ZTEL019 or ZTEL020.
    /// </summary>
    private static EquatableArray<string> BuildBuckets(
        Compilation compilation,
        AttributeData attr,
        IMethodSymbol method,
        string shortName,
        string metric,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        TypedConstant? found = null;
        foreach (var named in attr.NamedArguments)
        {
            if (string.Equals(named.Key, "Buckets", StringComparison.Ordinal))
                found = named.Value;
        }

        if (found is not { IsNull: false, Kind: TypedConstantKind.Array } buckets)
            return default;

        var location = AttributeLocations.Named(attr, "Buckets", MethodLocation(method));
        var values = buckets.Values.Select(static v => v.Value is double d ? d : double.NaN).ToArray();
        if (BucketProblem(values) is { } problem)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.InvalidHistogramBuckets, location, shortName, metric, problem));
            return default;
        }

        if (compilation.GetTypesByMetadataName("System.Diagnostics.Metrics.InstrumentAdvice`1").IsEmpty)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.HistogramBucketsUnavailable, location, shortName, metric));
            return default;
        }

        return new EquatableArray<string>(values
            .Select(static v => v.ToString("R", System.Globalization.CultureInfo.InvariantCulture))
            .ToImmutableArray());
    }

    /// <summary>What <c>InstrumentAdvice</c> would reject the boundaries for, or null when it accepts them.</summary>
    private static string? BucketProblem(double[] values)
    {
        if (values.Length == 0)
            return "are empty";

        for (var i = 0; i < values.Length; i++)
        {
            if (double.IsNaN(values[i]) || double.IsInfinity(values[i]))
                return "contain a value that is not finite";

            if (i > 0 && values[i] <= values[i - 1])
            {
                return "are not in strictly increasing order: "
                    + values[i].ToString("R", System.Globalization.CultureInfo.InvariantCulture)
                    + " follows "
                    + values[i - 1].ToString("R", System.Globalization.CultureInfo.InvariantCulture);
            }
        }

        return null;
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
        var each = kind == MetricKind.Histogram && GetNamedBool(attr, "Each");
        var eachShape = default(EachShape);
        if (!valueOk)
        {
            diagnostics.Add(PathNotFound(memberLocation, member, path));
        }
        else if (MisfitReason(compilation, kind, each, path, out eachShape) is { } reason)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.MemberDoesNotFitInstrument,
                memberLocation,
                each ? shortName + "(Each = true)" : shortName,
                member.Length == 0 ? "the return value" : $"'{member}'",
                path.FinalType!.ToDisplayString(),
                reason));
            valueOk = false;
        }

        // Checked even after a bad member, so one build reports every error on the attribute.
        var guardOk = TryBuildGuard(
            compilation, attr, method, resultType, GetNamedString(attr, "When"), diagnostics, out var guard);

        var buckets = kind == MetricKind.Histogram
            ? BuildBuckets(compilation, attr, method, shortName, metric, diagnostics)
            : default;

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
            !each && PathResolver.UnwrapNullable(path.FinalType!).SpecialType == SpecialType.System_Decimal,
            Buckets: buckets,
            Each: eachShape.Kind,
            ElementCanBeNull: eachShape.ElementCanBeNull,
            ElementIsDecimal: eachShape.ElementIsDecimal);
    }

    /// <summary>
    /// Why the resolved member cannot go to the instrument, for ZTEL009, or null when it fits.
    /// </summary>
    private static string? MisfitReason(
        Compilation compilation, MetricKind kind, bool each, PathResolver.Resolution path, out EachShape eachShape)
    {
        eachShape = default;
        if (!each)
            return Fits(kind, path.FinalType!) ? null : FitReason(kind, path.FinalType!);

        eachShape = ClassifyEach(compilation, path.FinalType!, path.CanBeNull, out var reason);
        return eachShape.Kind == EachKind.None ? reason : null;
    }

    private readonly record struct EachShape(EachKind Kind, bool ElementCanBeNull, bool ElementIsDecimal);

    private const string EachTypesReason =
        "With Each = true the member must be a ReadOnlySpan<double>, Span<double>, ReadOnlyMemory<double>, Memory<double>, an array, or a type whose GetEnumerator() returns a struct, with numeric elements";

    private const string EachSpanThroughNullReason =
        "A span cannot be read through a value that can be null; reach it through members that cannot be null, or expose it as a ReadOnlyMemory<double>";

    /// <summary>
    /// How a per-element histogram iterates <paramref name="type"/>, or <see cref="EachKind.None"/>
    /// with the reason when it cannot without allocating.
    /// </summary>
    private static EachShape ClassifyEach(Compilation compilation, ITypeSymbol type, bool canBeNull, out string reason)
    {
        reason = EachTypesReason;
        var underlying = PathResolver.UnwrapNullable(type);

        if (underlying is INamedTypeSymbol { TypeArguments.Length: 1 } named
            && named.TypeArguments[0].SpecialType == SpecialType.System_Double
            && string.Equals(named.ContainingNamespace?.ToDisplayString(), "System", StringComparison.Ordinal))
        {
            switch (named.OriginalDefinition.MetadataName)
            {
                case "ReadOnlySpan`1":
                case "Span`1":
                    if (!canBeNull)
                        return new EachShape(EachKind.Span, false, false);

                    reason = EachSpanThroughNullReason;
                    return default;

                case "ReadOnlyMemory`1":
                case "Memory`1":
                    return new EachShape(EachKind.Memory, false, false);
            }
        }

        var element = underlying is IArrayTypeSymbol { Rank: 1 } array
            ? array.ElementType
            : StructEnumeratorElement(compilation, underlying);

        if (element is null || !Fits(MetricKind.Histogram, element))
            return default;

        return new EachShape(
            EachKind.Enumerable,
            PathResolver.CanBeNull(element),
            PathResolver.UnwrapNullable(element).SpecialType == SpecialType.System_Decimal);
    }

    /// <summary>
    /// The element type of a <c>foreach</c> over <paramref name="type"/> when its public
    /// <c>GetEnumerator()</c> returns a struct, which <c>foreach</c> uses without boxing. A
    /// ref struct enumerator is left out: it cannot live in the proxy's async method.
    /// </summary>
    private static ITypeSymbol? StructEnumeratorElement(Compilation compilation, ITypeSymbol type)
    {
        foreach (var member in type.GetMembers("GetEnumerator"))
        {
            if (member is not IMethodSymbol { IsStatic: false, Parameters.Length: 0, TypeParameters.Length: 0 } getEnumerator
                || !compilation.IsSymbolAccessibleWithin(getEnumerator, compilation.Assembly))
            {
                continue;
            }

            var enumerator = getEnumerator.ReturnType;
            if (!enumerator.IsValueType || enumerator.IsRefLikeType)
                return null;

            return HasMoveNext(enumerator) ? CurrentType(enumerator) : null;
        }

        return null;
    }

    private static bool HasMoveNext(ITypeSymbol enumerator)
    {
        foreach (var member in enumerator.GetMembers("MoveNext"))
        {
            if (member is IMethodSymbol { Parameters.Length: 0, ReturnType.SpecialType: SpecialType.System_Boolean })
                return true;
        }

        return false;
    }

    private static ITypeSymbol? CurrentType(ITypeSymbol enumerator)
    {
        foreach (var member in enumerator.GetMembers("Current"))
        {
            if (member is IPropertySymbol property)
                return property.Type;
        }

        return null;
    }

    private static bool GetNamedBool(AttributeData attr, string name)
    {
        foreach (var named in attr.NamedArguments)
        {
            if (string.Equals(named.Key, name, StringComparison.Ordinal))
                return named.Value.Value is true;
        }

        return false;
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
    /// Builds the metric tags in emission order: <c>[MetricTagConstant]</c> in attribute order,
    /// <c>[MetricTag]</c> in parameter order, then <c>[MetricTagFromResult]</c> in attribute order.
    /// One that cannot be emitted, or that no declared metric would carry, is reported and left out.
    /// </summary>
    /// <remarks>
    /// The path and guard are resolved even for a tag reported as ZTEL011 or ZTEL012, so one build
    /// reports every error on the attribute. A tag name is unique per metric across all three kinds.
    /// </remarks>
    private static MetricTagModel[] BuildMetricTags(
        Compilation compilation,
        INamedTypeSymbol target,
        IMethodSymbol method,
        ITypeSymbol resultType,
        bool returnsVoid,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var builder = new MetricTagBuilder(compilation, target, method, diagnostics);
        builder.AddConstants();
        builder.AddParameterTags();
        if (returnsVoid)
            builder.ReportResultTagsOnVoid();
        else
            builder.AddResultTags(resultType);

        return builder.Tags.ToArray();
    }

    /// <summary>Collects one method's metric tags; see <see cref="BuildMetricTags"/>.</summary>
    private sealed class MetricTagBuilder
    {
        private readonly Compilation _compilation;
        private readonly INamedTypeSymbol _target;
        private readonly IMethodSymbol _method;
        private readonly ImmutableArray<DiagnosticInfo>.Builder _diagnostics;
        private readonly HashSet<(string Metric, string Tag)> _taken = new();
        private string[]? _declared;

        public MetricTagBuilder(
            Compilation compilation, INamedTypeSymbol target, IMethodSymbol method, ImmutableArray<DiagnosticInfo>.Builder diagnostics)
        {
            _compilation = compilation;
            _target = target;
            _method = method;
            _diagnostics = diagnostics;
        }

        public List<MetricTagModel> Tags { get; } = new();

        public void AddConstants()
        {
            foreach (var attr in _method.GetAttributes())
            {
                if (!IsAttribute(attr, MetricTagConstantAttrFqn) || Name(attr) is not { } name || attr.ConstructorArguments.Length < 2)
                    continue;

                var literal = FormatConstant(attr.ConstructorArguments[1]);
                if (literal is null)
                    _diagnostics.Add(UnsupportedConstant(attr, _target, _method, "MetricTagConstant", name));

                var filter = GetNamedString(attr, "Metric");
                if (!Place(attr, "MetricTagConstant", name, filter, MethodLocation(_method)) || literal is null)
                    continue;

                // A null constant adds no tag, as a null member does not.
                if (!string.Equals(literal, "null", StringComparison.Ordinal))
                    Tags.Add(new MetricTagModel(name, filter, string.Empty, false, Root: literal));
            }
        }

        public void AddParameterTags()
        {
            foreach (var parameter in _method.Parameters)
            {
                var fallback = parameter.Locations.FirstOrDefault() ?? MethodLocation(_method);
                foreach (var attr in parameter.GetAttributes())
                {
                    if (!IsAttribute(attr, MetricTagAttrFqn) || Name(attr) is not { } name)
                        continue;

                    var member = attr.ConstructorArguments.Length > 1 ? attr.ConstructorArguments[1].Value as string : null;
                    var filter = GetNamedString(attr, "Metric");
                    var placed = Place(attr, "MetricTag", name, filter, fallback);

                    var path = PathResolver.Resolve(_compilation, parameter.Type, member ?? string.Empty);
                    if (!path.Resolved)
                    {
                        _diagnostics.Add(ParameterTagPathNotFound(
                            AttributeLocations.Positional(attr, 1, "member", fallback), member!, parameter.Name, path, "MetricTag"));
                    }
                    else if (ProxyMembers.UnreadableForMetric(_method, parameter, path.FinalType!) is { } unreadable)
                    {
                        _diagnostics.Add(DiagnosticInfo.Create(
                            InstrumentDiagnostics.UnreadableParameter,
                            attr.ApplicationSyntaxReference?.GetSyntax().GetLocation() ?? fallback,
                            "MetricTag", parameter.Name, _target.Name, _method.Name, unreadable));
                        continue;
                    }

                    if (placed && path.Resolved)
                    {
                        Tags.Add(new MetricTagModel(
                            name, filter, path.Access!, path.CanBeNull, Root: TypeDeclarations.Identifier(parameter.Name),
                            RootIsOut: parameter.RefKind == RefKind.Out));
                    }
                }
            }
        }

        /// <summary>ZTEL005 for each <c>[MetricTagFromResult]</c>: there is no result to read.</summary>
        public void ReportResultTagsOnVoid()
        {
            foreach (var attr in _method.GetAttributes())
            {
                if (IsAttribute(attr, MetricTagFromResultAttrFqn) && Name(attr) is not null && attr.ConstructorArguments.Length >= 2)
                {
                    _diagnostics.Add(DiagnosticInfo.Create(
                        InstrumentDiagnostics.ResultReadOnVoidMethod,
                        MethodLocation(_method), "MetricTagFromResult", _target.ToDisplayString(), _method.Name));
                }
            }
        }

        public void AddResultTags(ITypeSymbol resultType)
        {
            foreach (var attr in _method.GetAttributes())
            {
                if (!IsAttribute(attr, MetricTagFromResultAttrFqn) || Name(attr) is not { } name || attr.ConstructorArguments.Length < 2)
                    continue;

                var filter = GetNamedString(attr, "Metric");
                var placed = Place(attr, "MetricTagFromResult", name, filter, MethodLocation(_method));

                var member = attr.ConstructorArguments[1].Value as string ?? string.Empty;
                var path = PathResolver.Resolve(_compilation, resultType, member);
                if (!path.Resolved)
                {
                    _diagnostics.Add(PathNotFound(
                        AttributeLocations.Positional(attr, 1, "member", MethodLocation(_method)), member, path));
                }

                var guardOk = TryBuildGuard(
                    _compilation, attr, _method, resultType, GetNamedString(attr, "When"), _diagnostics, out var guard);

                if (placed && path.Resolved && guardOk)
                    Tags.Add(new MetricTagModel(name, filter, path.Access!, path.CanBeNull, guard));
            }
        }

        private static string? Name(AttributeData attr) =>
            attr.ConstructorArguments.Length > 0 && attr.ConstructorArguments[0].Value is string { Length: > 0 } name
                ? name
                : null;

        /// <summary>Claims the name on the metrics the tag goes on; false when it goes on none of them.</summary>
        private bool Place(AttributeData attr, string shortName, string name, string? filter, Location fallback)
        {
            _declared ??= DeclaredMetrics(_method);
            var applies = ApplicableMetrics(attr, _target, _method, shortName, name, filter, _declared, fallback, _diagnostics);
            var unique = IsUniqueOnItsMetrics(attr, _target, _method, shortName, name, applies, _taken, fallback, _diagnostics);
            return applies.Length > 0 && unique;
        }
    }

    /// <summary>
    /// The declared metrics the tag goes on. Empty, having reported ZTEL011, when the method
    /// declares none or <paramref name="filter"/> names none of them.
    /// </summary>
    private static string[] ApplicableMetrics(
        AttributeData attr,
        INamedTypeSymbol target,
        IMethodSymbol method,
        string shortName,
        string name,
        string? filter,
        string[] declared,
        Location fallback,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var tagText = Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(name, quote: true);

        if (declared.Length == 0)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.MetricTagWithoutMetric,
                AttributeLocations.Positional(attr, 0, "name", fallback),
                tagText, target.ToDisplayString(), method.Name, shortName));
            return [];
        }

        if (filter is null)
            return declared;

        if (declared.Contains(filter, StringComparer.Ordinal))
            return [filter];

        diagnostics.Add(DiagnosticInfo.Create(
            InstrumentDiagnostics.MetricTagUnknownMetric,
            AttributeLocations.Named(attr, "Metric", fallback),
            tagText, target.ToDisplayString(), method.Name, filter, shortName));
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
        string shortName,
        string name,
        string[] metrics,
        HashSet<(string Metric, string Tag)> taken,
        Location fallback,
        ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        foreach (var metric in metrics)
        {
            if (!taken.Contains((metric, name)))
                continue;

            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.DuplicateMetricTag,
                AttributeLocations.Positional(attr, 0, "name", fallback),
                name, metric, target.ToDisplayString(), method.Name, shortName));
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
    /// <param name="Bases">The interfaces the instrumented interface extends, whose members the proxy implements.</param>
    private readonly record struct ParseResult(
        InstrumentModel? Model, EquatableArray<DiagnosticInfo> Diagnostics, ProxyFile? File, EquatableArray<string> Bases = default);
}
