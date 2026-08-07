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

            var returnType  = member.ReturnType.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
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
                ps[i].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
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

            (tags ??= new List<ResultTagModel>()).Add(new ResultTagModel(name!, member));
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
