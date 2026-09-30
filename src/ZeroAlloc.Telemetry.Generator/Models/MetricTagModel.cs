namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>One <c>[MetricTagFromResult]</c> that resolved and type-checked.</summary>
/// <param name="TagName">Tag key added to each measurement.</param>
/// <param name="Metric">
/// The metric name the tag is restricted to, or null for every metric the method records.
/// </param>
/// <param name="Access">
/// The resolved member access to append to the result root; empty reads the root itself.
/// </param>
/// <param name="ValueCanBeNull">
/// Whether the value can be null. It is then read with <c>is { } _metricTagN_K</c>, so a null
/// adds no tag.
/// </param>
/// <param name="GuardExpression">
/// The resolved <c>When</c> condition to append to the result root, such as
/// <c>?.IsSuccess == true</c>. Null adds the tag unconditionally.
/// </param>
internal sealed record MetricTagModel(
    string TagName,
    string? Metric,
    string Access,
    bool ValueCanBeNull,
    string? GuardExpression = null)
{
    /// <summary>Whether the tag goes on the instruments of <paramref name="metric"/>.</summary>
    public bool AppliesTo(MetricModel metric) =>
        Metric is null || string.Equals(Metric, metric.Metric, StringComparison.Ordinal);
}
