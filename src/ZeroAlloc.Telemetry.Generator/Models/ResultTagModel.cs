namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="TagName">Tag key written to the span.</param>
/// <param name="Member">
/// Member of the result to read, e.g. <c>Count</c>. Null or empty records the result itself.
/// </param>
internal sealed record ResultTagModel(string TagName, string? Member);
