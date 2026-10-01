namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>One <c>[TraceTag]</c> on a parameter. A parameter may carry several.</summary>
/// <param name="TagName">Tag key written to the span.</param>
/// <param name="AccessSuffix">
/// The member access as it should be emitted, with the operator for each segment already chosen
/// from the resolved types — e.g. <c>?.DocumentId?.Value</c> or <c>.Length</c>. Null or empty
/// records the argument itself. A path that does not resolve has no model at all: it is reported as
/// ZTEL010 and nothing is tagged.
/// </param>
/// <param name="NeedsCopy">
/// Whether the tag must read from a copy of the argument rather than the argument itself.
/// <para>
/// Roslyn treats <c>arg?.Member</c> as a null test on <c>arg</c>, leaving it maybe-null for the
/// rest of the method — and the argument is then forwarded to the inner call, which would raise
/// CS8604 in any consumer with nullable warnings enabled. Reading a copy keeps the forwarded
/// argument's null-state intact.
/// </para>
/// </param>
/// <param name="CanBeNull">Whether the tagged value can be null, so a tag passed at the span's start skips it.</param>
internal sealed record TraceTagModel(string TagName, string? AccessSuffix, bool NeedsCopy, bool CanBeNull);
