namespace ZeroAlloc.Telemetry.Generator.Models;

internal sealed record InstrumentModel(
    string? Namespace,
    string InterfaceName,
    string ProxyName,
    string ActivitySourceName,
    IReadOnlyList<MethodModel> Methods
);
