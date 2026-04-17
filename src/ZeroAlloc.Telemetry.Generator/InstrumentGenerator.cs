using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZeroAlloc.Telemetry.Generator.Models;

namespace ZeroAlloc.Telemetry.Generator;

[Generator]
public sealed class InstrumentGenerator : IIncrementalGenerator
{
    private const string InstrumentAttributeFqn = "ZeroAlloc.Telemetry.InstrumentAttribute";
    private const string TraceAttributeFqn      = "ZeroAlloc.Telemetry.TraceAttribute";
    private const string CountAttributeFqn      = "ZeroAlloc.Telemetry.CountAttribute";
    private const string HistogramAttributeFqn  = "ZeroAlloc.Telemetry.HistogramAttribute";

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                InstrumentAttributeFqn,
                predicate: static (node, _) => node is InterfaceDeclarationSyntax,
                transform: static (ctx, _) => Parse(ctx))
            .Where(static m => m is not null)
            .Select(static (m, _) => m!);

        context.RegisterSourceOutput(models, Emit);
    }

    private static InstrumentModel? Parse(GeneratorAttributeSyntaxContext ctx)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol iface) return null;

        var instrumentAttr = ctx.Attributes[0];

        // ActivitySource is passed as the first positional constructor argument
        var activitySource = instrumentAttr.ConstructorArguments.Length > 0
            ? instrumentAttr.ConstructorArguments[0].Value as string ?? string.Empty
            : string.Empty;

        var members = iface.GetMembers().OfType<IMethodSymbol>();
        var methods = new List<MethodModel>();

        foreach (var member in members)
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

            methods.Add(new MethodModel(
                member.Name,
                returnType,
                isAsync,
                returnsVoid,
                parameters,
                traceName,
                countMetric,
                histMetric));
        }

        var ns        = iface.ContainingNamespace.IsGlobalNamespace ? null : iface.ContainingNamespace.ToDisplayString();
        var ifaceName = iface.Name;
        var proxyName = (ifaceName.StartsWith("I", StringComparison.Ordinal) && ifaceName.Length > 1)
                        ? ifaceName.Substring(1) + "Instrumented"
                        : ifaceName + "Instrumented";

        return new InstrumentModel(ns, ifaceName, proxyName, activitySource, methods);
    }

    private static ParameterModel[] BuildParameters(IMethodSymbol method)
    {
        var ps = method.Parameters;
        var result = new ParameterModel[ps.Length];
        for (var i = 0; i < ps.Length; i++)
        {
            result[i] = new ParameterModel(
                ps[i].Type.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
                ps[i].Name);
        }

        return result;
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

    private static void Emit(SourceProductionContext ctx, InstrumentModel model)
    {
        var source   = ProxyWriter.Write(model);
        var hintName = model.Namespace is null
            ? $"{model.ProxyName}.g.cs"
            : $"{model.Namespace}_{model.ProxyName}.g.cs";
        ctx.AddSource(hintName, source);
    }
}
