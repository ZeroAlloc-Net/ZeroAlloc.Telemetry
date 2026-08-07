namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="Type">Fully-qualified parameter type.</param>
/// <param name="Name">Parameter name, used both in the signature and in the forwarded call.</param>
/// <param name="TagName">
/// Tag key from <c>[TraceTag]</c>, or null when the parameter is not recorded on the span.
/// </param>
/// <param name="TagAccessSuffix">
/// The member access as it should be emitted, with the operator for each segment already chosen
/// from the resolved types — e.g. <c>?.DocumentId?.Value</c> or <c>.Length</c>. Empty records the
/// argument itself; null when the path could not be resolved against the parameter type.
/// </param>
/// <param name="TagNeedsCopy">
/// Whether the tag must read from a copy of the argument rather than the argument itself.
/// <para>
/// Roslyn treats <c>arg?.Member</c> as a null test on <c>arg</c>, leaving it maybe-null for the
/// rest of the method — and the argument is then forwarded to the inner call, which would raise
/// CS8604 in any consumer with nullable warnings enabled. Reading a copy keeps the forwarded
/// argument's null-state intact.
/// </para>
/// </param>
internal sealed record ParameterModel(
    string Type,
    string Name,
    string? TagName = null,
    string? TagAccessSuffix = null,
    bool TagNeedsCopy = false);
