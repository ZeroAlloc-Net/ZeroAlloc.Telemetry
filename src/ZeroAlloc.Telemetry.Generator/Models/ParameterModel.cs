namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="Type">Fully-qualified parameter type.</param>
/// <param name="Name">Parameter name, used both in the signature and in the forwarded call.</param>
/// <param name="TagName">
/// Tag key from <c>[TraceTag]</c>, or null when the parameter is not recorded on the span.
/// </param>
/// <param name="TagAccessSuffix">
/// The member access as it should be emitted, with the operator for each segment already chosen
/// from the resolved types — e.g. <c>?.DocumentId?.Value</c> or <c>.Length</c>. Null or empty
/// records the argument itself. A path that does not resolve has no model at all: it is reported as
/// ZTEL010 and <paramref name="TagName"/> is null, so nothing is tagged.
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
/// <param name="TagCanBeNull">Whether the tagged value can be null, so a tag passed at the span's start skips it.</param>
/// <param name="Modifier">
/// The modifiers in the declaration, such as <c>ref </c>, <c>scoped </c> or <c>params </c>, with
/// the parameter's nullability attributes before them; empty for a plain parameter.
/// </param>
/// <param name="ArgumentModifier">The modifier in a forwarded call: <c>ref </c>, <c>out </c>, <c>in </c> or empty.</param>
/// <param name="IsOut">An <c>out</c> parameter, which has no value until the call returns.</param>
/// <param name="IsRefLike">A ref struct, which cannot be boxed or live in an async method.</param>
internal sealed record ParameterModel(
    string Type,
    string Name,
    string? TagName = null,
    string? TagAccessSuffix = null,
    bool TagNeedsCopy = false,
    bool TagCanBeNull = false,
    string Modifier = "",
    string ArgumentModifier = "",
    bool IsOut = false,
    bool IsRefLike = false)
{
    /// <summary>Whether an async method can take the parameter: no modifier that makes it a reference, and not a ref struct.</summary>
    public bool FitsAsync => ArgumentModifier.Length == 0 && !IsRefLike;
}
