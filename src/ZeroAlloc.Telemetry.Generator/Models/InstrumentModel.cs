namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="HintName">
/// The name of the generated file, from <see cref="HintNames.ForInterface"/>. Computed while
/// parsing, since emitting has no symbols, and kept as a string so the model stays equatable.
/// </param>
internal sealed record InstrumentModel(
    string HintName,
    string? Namespace,
    string InterfaceName,
    string ProxyName,
    string ActivitySourceName,
    EquatableArray<MethodModel> Methods,
    bool PublicProxy = false
);
