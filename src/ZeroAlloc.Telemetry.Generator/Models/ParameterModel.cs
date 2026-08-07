namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="Type">Fully-qualified parameter type.</param>
/// <param name="Name">Parameter name, used both in the signature and in the forwarded call.</param>
/// <param name="TagName">
/// Tag key from <c>[TraceTag]</c>, or null when the parameter is not recorded on the span.
/// </param>
internal sealed record ParameterModel(string Type, string Name, string? TagName = null);
