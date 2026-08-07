namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="TagName">Tag key written to the span.</param>
/// <param name="Member">
/// Member of the result to read, e.g. <c>Count</c>. Null or empty records the result itself.
/// </param>
/// <param name="AccessSuffix">
/// The member access as it should be emitted, with the operator for each segment already chosen
/// from the resolved types — e.g. <c>?.Value?.Count</c> or <c>.Length</c>. Null when there is no
/// member, or when the path could not be resolved against the result type.
/// <para>
/// Pre-resolving matters because the operator cannot be decided from the path text alone:
/// <c>?.</c> is required wherever the preceding value may be null, and is a compile error
/// wherever it cannot be.
/// </para>
/// </param>
/// <param name="GuardExpression">
/// The condition the tag is emitted under, already resolved and null-tolerant — e.g.
/// <c>?.IsSuccess == true</c> appended to the root. Null records unconditionally.
/// </param>
internal sealed record ResultTagModel(
    string TagName,
    string? Member,
    string? AccessSuffix = null,
    string? GuardExpression = null);
