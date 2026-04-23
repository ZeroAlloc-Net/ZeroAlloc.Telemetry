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
        title: "[Trace]/[Count]/[Histogram] on a method in a type without [Instrument] is ignored",
        messageFormat: "[{0}] on '{1}.{2}' is ignored — the containing type does not have [Instrument] applied, so no proxy is generated. Either apply [Instrument] to the enclosing interface or remove this attribute.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
