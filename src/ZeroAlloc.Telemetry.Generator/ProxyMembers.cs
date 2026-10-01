using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using ZeroAlloc.Telemetry.Generator.Models;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// The interface members a proxy has to implement, and how each is declared. Every shape C#
/// allows in an interface either compiles in the proxy or is reported, so a consumer never gets
/// an error inside generated code.
/// </summary>
internal static class ProxyMembers
{
    private const string CodeAnalysisNamespace = "System.Diagnostics.CodeAnalysis";

    private static readonly string[] InstrumentationAttributes =
    [
        "ZeroAlloc.Telemetry.TraceAttribute",
        "ZeroAlloc.Telemetry.CountAttribute",
        "ZeroAlloc.Telemetry.HistogramAttribute",
        "ZeroAlloc.Telemetry.CountFromResultAttribute",
        "ZeroAlloc.Telemetry.HistogramFromResultAttribute",
        "ZeroAlloc.Telemetry.MetricTagFromResultAttribute",
        "ZeroAlloc.Telemetry.MetricTagConstantAttribute",
        "ZeroAlloc.Telemetry.TraceTagFromResultAttribute",
        "ZeroAlloc.Telemetry.TraceTagConstantAttribute",
    ];

    public const string OutParameter = "an out parameter has no value until the call returns";

    public const string RefStructValue = "a ref struct cannot be boxed into a tag value";

    public const string RefStructPastAwait = "a ref struct argument cannot be carried past the await of an async method";

    /// <summary>
    /// The instance members the proxy implements: the interface's own, then those of the
    /// interfaces it extends, which a class implementing it has to implement too. A member
    /// declared twice with one signature, in a diamond, is implemented once.
    /// </summary>
    private static IEnumerable<ISymbol> InstanceMembers(INamedTypeSymbol target)
    {
        foreach (var member in target.GetMembers())
            yield return member;

        foreach (var inherited in target.AllInterfaces)
        {
            foreach (var member in inherited.GetMembers())
                yield return member;
        }
    }

    /// <summary>
    /// What a member is forwarded to: <c>_inner</c> for the interface's own members, and
    /// <c>_inner</c> cast to the declaring interface for an inherited one, which two base
    /// interfaces may both declare.
    /// </summary>
    public static string Receiver(INamedTypeSymbol target, ISymbol member, SymbolDisplayFormat typeFormat) =>
        SymbolEqualityComparer.Default.Equals(member.ContainingType, target)
            ? "_inner"
            : $"(({member.ContainingType.ToDisplayString(typeFormat)})_inner)";

    /// <summary>The ordinary methods to implement, without accessors, statics or duplicates.</summary>
    public static IEnumerable<IMethodSymbol> Methods(INamedTypeSymbol target)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var member in InstanceMembers(target))
        {
            if (member is IMethodSymbol { MethodKind: MethodKind.Ordinary, IsStatic: false } method
                && seen.Add(Signature(method)))
            {
                yield return method;
            }
        }
    }

    /// <summary>A static abstract member of the interface or one it extends, or null.</summary>
    public static ISymbol? FirstStaticAbstract(INamedTypeSymbol target)
    {
        foreach (var member in InstanceMembers(target))
        {
            if (member.IsStatic && member.IsAbstract)
                return member;
        }

        return null;
    }

    /// <summary>
    /// The properties, indexers and events, forwarded to the wrapped instance. An
    /// instrumentation attribute on one of their accessors is reported as ZTEL023: there is no
    /// call to time or trace that the member's caller would recognise.
    /// </summary>
    public static EquatableArray<PropertyModel> BuildProperties(
        INamedTypeSymbol target, SymbolDisplayFormat typeFormat, ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var properties = ImmutableArray.CreateBuilder<PropertyModel>();
        foreach (var member in InstanceMembers(target))
        {
            if (member.IsStatic)
                continue;

            if (member is IPropertySymbol property && seen.Add("p:" + property.Name + Parameters(property.Parameters)))
            {
                ReportInstrumentedAccessor(target, property.GetMethod, diagnostics);
                ReportInstrumentedAccessor(target, property.SetMethod, diagnostics);
                properties.Add(new PropertyModel(
                    property.IsIndexer ? PropertyKind.Indexer : PropertyKind.Property,
                    property.Type.ToDisplayString(typeFormat),
                    TypeDeclarations.Identifier(property.Name),
                    new EquatableArray<ParameterModel>(property.Parameters.Select(p => Parameter(p, typeFormat)).ToImmutableArray()),
                    property.GetMethod is not null,
                    property.SetMethod is not null,
                    property.SetMethod?.IsInitOnly == true,
                    RefPrefix(property.ReturnsByRef, property.ReturnsByRefReadonly),
                    Receiver(target, property, typeFormat)));
            }
            else if (member is IEventSymbol @event && seen.Add("e:" + @event.Name))
            {
                ReportInstrumentedAccessor(target, @event.AddMethod, diagnostics);
                ReportInstrumentedAccessor(target, @event.RemoveMethod, diagnostics);
                properties.Add(new PropertyModel(
                    PropertyKind.Event,
                    @event.Type.ToDisplayString(typeFormat),
                    TypeDeclarations.Identifier(@event.Name),
                    EquatableArray<ParameterModel>.Empty,
                    false,
                    false,
                    false,
                    string.Empty,
                    Receiver(target, @event, typeFormat)));
            }
        }

        return new EquatableArray<PropertyModel>(properties.ToImmutable());
    }

    /// <summary>
    /// A method returning by reference, forwarded without instrumentation. The reference would
    /// have to outlive the try block that times and traces the call, which C# does not allow.
    /// </summary>
    public static MethodModel RefReturning(
        INamedTypeSymbol target, IMethodSymbol method, SymbolDisplayFormat typeFormat, ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (FirstInstrumentation(method) is { } attribute)
        {
            diagnostics.Add(DiagnosticInfo.Create(
                InstrumentDiagnostics.UninstrumentableMember,
                method.Locations.FirstOrDefault(),
                attribute, target.Name, method.Name, "a method that returns by reference cannot be instrumented"));
        }

        return new MethodModel(
            method.Name,
            method.ReturnType.ToDisplayString(typeFormat),
            false,
            false,
            new EquatableArray<ParameterModel>(method.Parameters.Select(p => Parameter(p, typeFormat)).ToImmutableArray()),
            null,
            null,
            null,
            EquatableArray<ResultTagModel>.Empty,
            false,
            EquatableArray<ConstantTagModel>.Empty,
            EquatableArray<MetricModel>.Empty,
            EquatableArray<MetricTagModel>.Empty,
            null,
            TypeDeclarations.TypeParameterList(method.TypeParameters),
            TypeDeclarations.ConstraintClauses(method.TypeParameters, typeFormat),
            RefReturn: RefPrefix(method.ReturnsByRef, method.ReturnsByRefReadonly),
            Receiver: Receiver(target, method, typeFormat));
    }

    /// <summary>Why a tag or name token cannot read the parameter when the span starts, or null.</summary>
    public static string? UnreadableAtStart(IParameterSymbol parameter) =>
        parameter.RefKind == RefKind.Out ? OutParameter : null;

    /// <summary>
    /// Why a metric tag cannot read <paramref name="parameter"/>, whose path ends at
    /// <paramref name="valueType"/>, or null. Metric tags are read after the call, so an out
    /// parameter has its value by then.
    /// </summary>
    public static string? UnreadableForMetric(IMethodSymbol method, IParameterSymbol parameter, ITypeSymbol valueType)
    {
        if (valueType.IsRefLikeType)
            return RefStructValue;

        return parameter.Type.IsRefLikeType && TaskShapes.IsAwaitable(method.ReturnType) ? RefStructPastAwait : null;
    }

    /// <summary>
    /// The declaration modifiers of <paramref name="parameter"/>, after its nullability attributes:
    /// an implementation has to repeat <c>ref</c>, <c>out</c>, <c>in</c> and <c>scoped</c>, and
    /// the attributes keep the caller's flow analysis, as on a <c>TryGet</c> pattern.
    /// </summary>
    public static string Modifier(IParameterSymbol parameter)
    {
        var sb = new StringBuilder();
        foreach (var attr in parameter.GetAttributes())
        {
            if (attr.AttributeClass is { } cls
                && string.Equals(cls.ContainingNamespace?.ToDisplayString(), CodeAnalysisNamespace, StringComparison.Ordinal)
                && FormatAttribute(attr) is { } text)
            {
                sb.Append(text).Append(' ');
            }
        }

        // An out parameter and a params span are scoped implicitly, and scoped may not precede params.
        if (parameter.ScopedKind != ScopedKind.None && parameter.RefKind != RefKind.Out && !parameter.IsParams)
            sb.Append("scoped ");

        if (parameter.IsParams)
            sb.Append("params ");

        sb.Append(parameter.RefKind switch
        {
            RefKind.Ref => "ref ",
            RefKind.Out => "out ",
            RefKind.In => "in ",
            RefKind.RefReadOnlyParameter => "ref readonly ",
            _ => string.Empty,
        });

        return sb.ToString();
    }

    /// <summary>The modifier an argument is forwarded with.</summary>
    public static string ArgumentModifier(IParameterSymbol parameter) => parameter.RefKind switch
    {
        RefKind.Ref => "ref ",
        RefKind.Out => "out ",
        RefKind.In or RefKind.RefReadOnlyParameter => "in ",
        _ => string.Empty,
    };

    private static ParameterModel Parameter(IParameterSymbol parameter, SymbolDisplayFormat typeFormat) =>
        new(
            parameter.Type.ToDisplayString(typeFormat),
            parameter.Name,
            Modifier: Modifier(parameter),
            ArgumentModifier: ArgumentModifier(parameter),
            IsOut: parameter.RefKind == RefKind.Out,
            IsRefLike: parameter.Type.IsRefLikeType);

    private static string RefPrefix(bool byRef, bool byRefReadonly) =>
        byRefReadonly ? "ref readonly " : byRef ? "ref " : string.Empty;

    private static void ReportInstrumentedAccessor(
        INamedTypeSymbol target, IMethodSymbol? accessor, ImmutableArray<DiagnosticInfo>.Builder diagnostics)
    {
        if (accessor is null || FirstInstrumentation(accessor) is not { } attribute)
            return;

        diagnostics.Add(DiagnosticInfo.Create(
            InstrumentDiagnostics.UninstrumentableMember,
            accessor.Locations.FirstOrDefault() ?? accessor.AssociatedSymbol?.Locations.FirstOrDefault(),
            attribute, target.Name, accessor.AssociatedSymbol?.Name ?? accessor.Name,
            "instrumentation is not supported on a property, indexer or event accessor"));
    }

    /// <summary>The short name of the first instrumentation attribute on the method, or null.</summary>
    private static string? FirstInstrumentation(IMethodSymbol method)
    {
        foreach (var attr in method.GetAttributes())
        {
            var name = attr.AttributeClass?.ToDisplayString();
            if (name is not null && Array.IndexOf(InstrumentationAttributes, name) >= 0)
                return attr.AttributeClass!.Name.Substring(0, attr.AttributeClass.Name.Length - "Attribute".Length);
        }

        return null;
    }

    /// <summary>The method's name, arity and parameter types, which decide whether two declarations are one member.</summary>
    private static string Signature(IMethodSymbol method) =>
        method.Name + "`" + method.Arity.ToString(System.Globalization.CultureInfo.InvariantCulture) + Parameters(method.Parameters);

    private static string Parameters(ImmutableArray<IParameterSymbol> parameters) =>
        "(" + string.Join(",", parameters.Select(static p => p.RefKind.ToString() + " " + p.Type.ToDisplayString())) + ")";

    /// <summary>A <c>System.Diagnostics.CodeAnalysis</c> attribute as written, or null when an argument cannot be.</summary>
    private static string? FormatAttribute(AttributeData attr)
    {
        var args = new List<string>();
        foreach (var argument in attr.ConstructorArguments)
        {
            switch (argument.Value)
            {
                case bool b:
                    args.Add(b ? "true" : "false");
                    break;
                case string s:
                    args.Add(Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(s, quote: true));
                    break;
                default:
                    return null;
            }
        }

        var name = "global::" + attr.AttributeClass!.ToDisplayString();
        return args.Count == 0 ? $"[{name}]" : $"[{name}({string.Join(", ", args)})]";
    }
}
