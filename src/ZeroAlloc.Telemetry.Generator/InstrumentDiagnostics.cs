using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator;

internal static class InstrumentDiagnostics
{
    private const string Category = "ZeroAlloc.Telemetry";

    public static readonly DiagnosticDescriptor InstrumentOnNonInterface = new(
        id: "ZTEL001",
        title: "[Instrument] only applies to interfaces",
        messageFormat: "[Instrument] cannot be applied to '{0}' — the generator emits a proxy class that implements the target, which only makes sense on an interface. Apply [Instrument] to an interface instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyActivitySource = new(
        id: "ZTEL002",
        title: "[Instrument] requires a non-empty ActivitySource name",
        messageFormat: "[Instrument] on '{0}' has an empty ActivitySource name. The generated proxy will create an ActivitySource with no name, which makes tracing subscriptions effectively unroutable. Supply a meaningful name — typically the fully-qualified service or module name.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodAttributeWithoutInstrument = new(
        id: "ZTEL003",
        title: "Telemetry attribute on a method in a type without [Instrument] is ignored",
        messageFormat: "[{0}] on '{1}.{2}' is ignored — the containing type does not have [Instrument] applied, so no proxy is generated. Either apply [Instrument] to the enclosing interface or remove this attribute.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TagWithoutTrace = new(
        id: "ZTEL004",
        title: "[TraceTag]/[TraceTagFromResult] without [Trace] records nothing",
        messageFormat: "[{0}] on '{1}.{2}' records nothing — the method has no [Trace], so no span is started to carry the tag. Add [Trace] to the method or remove the tag.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ResultReadOnVoidMethod = new(
        id: "ZTEL005",
        title: "Result-reading attribute on a method with no return value",
        messageFormat: "[{0}] on '{1}.{2}' records nothing — the method returns void, Task or ValueTask, so there is no result to read. Remove it or return a value.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownSpanNameToken = new(
        id: "ZTEL006",
        title: "Unrecognised token in a [Trace] span name",
        messageFormat: "'{0}' in the [Trace] name on '{1}.{2}' is not a recognised token and is emitted verbatim, so the span name will contain a literal brace. The only supported token is {{type}}, which substitutes the wrapped implementation's type name.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberPathNotFound = new(
        id: "ZTEL007",
        title: "Member path does not resolve",
        messageFormat: "'{0}' in the path '{1}' is not a readable instance property or field of '{2}'. Each segment of a member path names a property or field of the type reached so far, starting from the awaited return value.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ZTEL007 for a path such as <c>Total..Count</c>. Same rule, but there is no segment name to
    /// quote, and a quoted empty name reads as a generator bug rather than a typo.
    /// </summary>
    public static readonly DiagnosticDescriptor MemberPathEmptySegment = new(
        id: "ZTEL007",
        title: "Member path does not resolve",
        messageFormat: "The path '{0}' has an empty segment. Each segment of a member path names a property or field of the type reached so far, starting from the awaited return value.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor WhenNotBoolean = new(
        id: "ZTEL008",
        title: "When must name a bool member",
        messageFormat: "When = '{0}' resolves to '{1}'. A guard must name a member of type bool or bool? on the awaited return value, such as IsSuccess.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberDoesNotFitInstrument = new(
        id: "ZTEL009",
        title: "Member type does not fit the instrument",
        messageFormat: "[{0}] cannot record {1} of type '{2}'. {3}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
}
