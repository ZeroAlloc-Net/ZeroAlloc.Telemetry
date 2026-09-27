namespace ZeroAlloc.Telemetry.Generator.Models;

internal sealed record InstrumentModel(
    string? Namespace,
    string InterfaceName,
    string ProxyName,
    string ActivitySourceName,
    EquatableArray<MethodModel> Methods,
    bool PublicProxy = false
);
