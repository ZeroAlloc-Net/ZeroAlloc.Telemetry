using System.Collections.Immutable;
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
            .Select(static (r, _) => r!.Value);

        // Report diagnostics collected during parse, then emit code only when
        // the target is valid.
        context.RegisterSourceOutput(results, static (ctx, result) =>
        {
            foreach (var diag in result.Diagnostics)
                ctx.ReportDiagnostic(diag);

            if (result.Model is { } model)
            {
                var source   = ProxyWriter.Write(model);
                var hintName = model.Namespace is null
                    ? $"{model.ProxyName}.g.cs"
                    : $"{model.Namespace}_{model.ProxyName}.g.cs";
                ctx.AddSource(hintName, source);
            }
        });

        // ZTEL003: method attributes on a method whose containing type lacks
        // [Instrument] are silently ignored. Scan every method carrying any of
        // the three per-method attributes and check the enclosing type.
        RegisterMethodAttributeDiagnostic(context, TraceAttributeFqn, "Trace");
        RegisterMethodAttributeDiagnostic(context, CountAttributeFqn, "Count");
        RegisterMethodAttributeDiagnostic(context, HistogramAttributeFqn, "Histogram");
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
                    if (ctx.TargetSymbol is not IMethodSymbol method) return (Diagnostic?)null;
                    var containing = method.ContainingType;
                    if (containing is null) return null;
                    foreach (var a in containing.GetAttributes())
                    {
                        if (string.Equals(a.AttributeClass?.ToDisplayString(), InstrumentAttributeFqn, StringComparison.Ordinal))
                            return null; // Container has [Instrument] — proxy is generated.
                    }
                    var loc = method.Locations.FirstOrDefault() ?? Location.None;
                    return Diagnostic.Create(
                        InstrumentDiagnostics.MethodAttributeWithoutInstrument,
                        loc,
                        shortName, containing.Name, method.Name);
                })
            .Where(static d => d is not null)
            .Select(static (d, _) => d!);

        context.RegisterSourceOutput(orphans, static (ctx, diag) => ctx.ReportDiagnostic(diag));
    }

    private static ParseResult? Parse(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol target) return null;

        var instrumentAttr = ctx.Attributes[0];
        var attrLocation = instrumentAttr.ApplicationSyntaxReference?.GetSyntax().GetLocation()
            ?? target.Locations.FirstOrDefault()
            ?? Location.None;

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

        // ZTEL001: the generator emits a proxy CLASS implementing the target.
        // That only makes sense when the target is an interface.
        if (target.TypeKind != TypeKind.Interface)
        {
            diagnostics.Add(Diagnostic.Create(
                InstrumentDiagnostics.InstrumentOnNonInterface,
                attrLocation,
                target.ToDisplayString()));
            return new ParseResult(null, diagnostics.ToImmutable());
        }

        // ActivitySource is the first positional constructor argument.
        var activitySource = instrumentAttr.ConstructorArguments.Length > 0
            ? instrumentAttr.ConstructorArguments[0].Value as string ?? string.Empty
            : string.Empty;

        // ZTEL002: empty ActivitySource leaves subscribers with nothing to match against.
        if (string.IsNullOrWhiteSpace(activitySource))
        {
            diagnostics.Add(Diagnostic.Create(
                InstrumentDiagnostics.EmptyActivitySource,
                attrLocation,
                target.ToDisplayString()));
            return new ParseResult(null, diagnostics.ToImmutable());
        }

        // PublicProxy is a named argument; absent means the default (internal proxy).
        var publicProxy = false;
        foreach (var named in instrumentAttr.NamedArguments)
        {
            if (string.Equals(named.Key, "PublicProxy", StringComparison.Ordinal))
                publicProxy = named.Value.Value is true;
        }

        var methods = BuildMethods(target, diagnostics);
        var ns        = target.ContainingNamespace.IsGlobalNamespace ? null : target.ContainingNamespace.ToDisplayString();
        var ifaceName = target.Name;
        var proxyName = (ifaceName.StartsWith("I", StringComparison.Ordinal) && ifaceName.Length > 1)
                        ? ifaceName.Substring(1) + "Instrumented"
                        : ifaceName + "Instrumented";

        return new ParseResult(
            new InstrumentModel(ns, ifaceName, proxyName, activitySource, methods, publicProxy),
            diagnostics.ToImmutable());
    }

    private static List<MethodModel> BuildMethods(
        INamedTypeSymbol target,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var methods = new List<MethodModel>();
        foreach (var member in target.GetMembers().OfType<IMethodSymbol>())
        {
            var traceName   = GetAttributeFirstArg(member, TraceAttributeFqn);
            var countMetric = GetAttributeFirstArg(member, CountAttributeFqn);
            var histMetric  = GetAttributeFirstArg(member, HistogramAttributeFqn);

            var returnType  = member.ReturnType.ToDisplayString(TypeFormat);
            var isAsync     = returnType.IndexOf("ValueTask", StringComparison.Ordinal) >= 0
                           || returnType.IndexOf("Task", StringComparison.Ordinal) >= 0;
            var returnsVoid = string.Equals(returnType, "global::System.Threading.Tasks.ValueTask", StringComparison.Ordinal)
                           || string.Equals(returnType, "global::System.Threading.Tasks.Task", StringComparison.Ordinal)
                           || string.Equals(returnType, "void", StringComparison.Ordinal);

            var parameters = BuildParameters(member);
            var resultTags = BuildResultTags(member);

            ReportTagDiagnostics(diagnostics, target, member, parameters, resultTags, traceName, returnsVoid);

            methods.Add(new MethodModel(
                member.Name,
                returnType,
                isAsync,
                returnsVoid,
                parameters,
                traceName,
                countMetric,
                histMetric,
                resultTags,
                ResultCanBeNull(member)));
        }
        return methods;
    }

    /// <summary>
    /// Whether the value a result tag reads from can be null, after unwrapping
    /// <c>Task&lt;T&gt;</c>/<c>ValueTask&lt;T&gt;</c>. Emitting <c>?.</c> against a non-nullable
    /// value type is a compile error, so the writer needs to know which operator to use.
    /// </summary>
    private static bool ResultCanBeNull(IMethodSymbol method)
    {
        var type = method.ReturnType;

        if (type is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } named)
        {
            var definition = named.OriginalDefinition.ToDisplayString();
            if (string.Equals(definition, "System.Threading.Tasks.Task<TResult>", StringComparison.Ordinal)
                || string.Equals(definition, "System.Threading.Tasks.ValueTask<TResult>", StringComparison.Ordinal))
            {
                type = named.TypeArguments[0];
            }
        }

        return type.IsReferenceType
            || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;
    }

    /// <summary>Unwraps <c>Task&lt;T&gt;</c>/<c>ValueTask&lt;T&gt;</c> to the awaited type.</summary>
    private static ITypeSymbol UnwrapAwaited(ITypeSymbol type)
    {
        if (type is INamedTypeSymbol { IsGenericType: true, TypeArguments.Length: 1 } named)
        {
            var definition = named.OriginalDefinition.ToDisplayString();
            if (string.Equals(definition, "System.Threading.Tasks.Task<TResult>", StringComparison.Ordinal)
                || string.Equals(definition, "System.Threading.Tasks.ValueTask<TResult>", StringComparison.Ordinal))
            {
                return named.TypeArguments[0];
            }
        }

        return type;
    }

    private static bool CanBeNull(ITypeSymbol type) =>
        type.IsReferenceType || type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    /// <summary>Returns T for <c>Nullable&lt;T&gt;</c>, otherwise the type unchanged.</summary>
    private static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T
            && type is INamedTypeSymbol { TypeArguments.Length: 1 } n
                ? n.TypeArguments[0]
                : type;

    /// <summary>
    /// Resolves a dotted member path against <paramref name="rootType"/> and returns it as it
    /// should be emitted — for example <c>?.Value?.Count</c>.
    /// </summary>
    /// <remarks>
    /// The operator for each segment cannot be chosen from the path text: <c>?.</c> is required
    /// wherever the preceding value may be null and is a compile error wherever it cannot be. So
    /// each segment is resolved to a symbol and the operator picked from the type before it.
    /// <para>
    /// Emitting <c>?.</c> only on the first segment — as this generator did — leaves every later
    /// segment unguarded, so a null part-way along a path throws from instrumentation. That is the
    /// one thing tagging must never do.
    /// </para>
    /// <para>
    /// Returns null when a segment cannot be resolved, which leaves the caller on its previous
    /// behaviour rather than emitting a guess. An unresolvable path is almost always a typo, and
    /// the generated code then fails to compile with the member name in the message.
    /// </para>
    /// </remarks>
    private static string? ResolveMemberAccess(ITypeSymbol rootType, string memberPath)
    {
        if (string.IsNullOrWhiteSpace(memberPath))
            return null;

        var current = rootType;
        var sb = new System.Text.StringBuilder();

        foreach (var rawSegment in memberPath.Split('.'))
        {
            var segment = rawSegment.Trim();
            if (segment.Length == 0)
                return null;

            var nullable = CanBeNull(current);
            var underlying = UnwrapNullable(current);

            // `x?.Value` on a Nullable<T> does not mean Nullable<T>.Value: the null-conditional
            // unwraps first, so the member is looked up on T and `.Value` fails to compile. The
            // segment is also redundant — the tag boxes to the same T-or-null either way — so
            // drop it and carry on from the underlying type.
            if (nullable && !ReferenceEquals(underlying, current)
                && string.Equals(segment, "Value", StringComparison.Ordinal))
            {
                current = underlying;
                continue;
            }

            sb.Append(nullable ? "?." : ".");
            sb.Append(segment);

            // Look up against the underlying type for the same reason: after `?.` on a
            // Nullable<T>, members resolve on T.
            var memberType = FindMemberType(underlying, segment);
            if (memberType is null)
                return null;

            current = memberType;
        }

        // May be empty when every segment resolved away — a bare "Value" on a nullable result.
        // That is a successful resolution meaning "tag the root itself", which is distinct from
        // the null returned above for a path that could not be resolved at all.
        return sb.ToString();
    }

    /// <summary>Finds a property or field by name, walking base types.</summary>
    private static ITypeSymbol? FindMemberType(ITypeSymbol type, string name)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            foreach (var m in t.GetMembers(name))
            {
                if (m is IPropertySymbol { Parameters.Length: 0 } p) return p.Type;
                if (m is IFieldSymbol f) return f.Type;
            }
        }

        // Interfaces do not inherit through BaseType, so check the full interface set too —
        // ICollection<T>.Count on an IReadOnlyList<T> parameter is the common case.
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var m in iface.GetMembers(name))
            {
                if (m is IPropertySymbol { Parameters.Length: 0 } p) return p.Type;
            }
        }

        return null;
    }

    /// <summary>
    /// A tag with nowhere to go is a silent no-op, which is worse than a build message: the
    /// telemetry simply never appears and the gap is only noticed downstream.
    /// </summary>
    private static void ReportTagDiagnostics(
        ImmutableArray<Diagnostic>.Builder diagnostics,
        INamedTypeSymbol target,
        IMethodSymbol member,
        IReadOnlyList<ParameterModel> parameters,
        IReadOnlyList<ResultTagModel> resultTags,
        string? traceName,
        bool returnsVoid)
    {
        var location = member.Locations.FirstOrDefault() ?? Location.None;

        if (traceName is null)
        {
            var hasParamTag = parameters.Any(p => p.TagName is not null);
            if (hasParamTag)
            {
                diagnostics.Add(Diagnostic.Create(
                    InstrumentDiagnostics.TagWithoutTrace,
                    location, "TraceTag", target.ToDisplayString(), member.Name));
            }

            if (resultTags.Count > 0)
            {
                diagnostics.Add(Diagnostic.Create(
                    InstrumentDiagnostics.TagWithoutTrace,
                    location, "TraceTagFromResult", target.ToDisplayString(), member.Name));
            }
        }

        // Reported independently of [Trace]: the attribute is wrong on a void method either way.
        if (returnsVoid && resultTags.Count > 0)
        {
            diagnostics.Add(Diagnostic.Create(
                InstrumentDiagnostics.ResultTagOnVoidMethod,
                location, target.ToDisplayString(), member.Name));
        }
    }

    private static ParameterModel[] BuildParameters(IMethodSymbol method)
    {
        var ps = method.Parameters;
        var result = new ParameterModel[ps.Length];
        for (var i = 0; i < ps.Length; i++)
        {
            result[i] = new ParameterModel(
                ps[i].Type.ToDisplayString(TypeFormat),
                ps[i].Name,
                GetTraceTagName(ps[i]));
        }

        return result;
    }

    private static string? GetTraceTagName(IParameterSymbol parameter)
    {
        foreach (var attr in parameter.GetAttributes())
        {
            if (!string.Equals(attr.AttributeClass?.ToDisplayString(), TraceTagAttributeFqn, StringComparison.Ordinal))
                continue;

            if (attr.ConstructorArguments.Length > 0)
                return attr.ConstructorArguments[0].Value as string;
        }

        return null;
    }

    private static ResultTagModel[] BuildResultTags(IMethodSymbol method)
    {
        List<ResultTagModel>? tags = null;
        foreach (var attr in method.GetAttributes())
        {
            if (!string.Equals(attr.AttributeClass?.ToDisplayString(), TraceTagFromResultAttrFqn, StringComparison.Ordinal))
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

            var accessSuffix = string.IsNullOrEmpty(member)
                ? null
                : ResolveMemberAccess(UnwrapAwaited(method.ReturnType), member!);

            (tags ??= new List<ResultTagModel>()).Add(new ResultTagModel(name!, member, accessSuffix));
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

    private readonly record struct ParseResult(InstrumentModel? Model, ImmutableArray<Diagnostic> Diagnostics);
}
