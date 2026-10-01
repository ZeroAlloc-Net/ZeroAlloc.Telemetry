namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>
/// One metric tag that resolved and type-checked: a <c>[MetricTagConstant]</c>, a
/// <c>[MetricTag]</c> on a parameter, or a <c>[MetricTagFromResult]</c>.
/// </summary>
/// <param name="TagName">Tag key added to each measurement.</param>
/// <param name="Metric">
/// The metric name the tag is restricted to, or null for every metric the method records.
/// </param>
/// <param name="Access">
/// The resolved member access to append to the root; empty reads the root itself.
/// </param>
/// <param name="ValueCanBeNull">
/// Whether the value can be null. It is then read with <c>is { } _metricTagN_K</c>, so a null
/// adds no tag.
/// </param>
/// <param name="GuardExpression">
/// The resolved <c>When</c> condition to append to the result root, such as
/// <c>?.IsSuccess == true</c>. Null adds the tag unconditionally.
/// </param>
/// <param name="RootIsOut">
/// Whether the root is an <c>out</c> parameter, which has no value on the throw path.
/// </param>
/// <param name="Root">
/// What <see cref="Access"/> is appended to: the parameter for a <c>[MetricTag]</c>, or the
/// literal for a <c>[MetricTagConstant]</c>. Null for a <c>[MetricTagFromResult]</c>, which reads
/// the result.
/// </param>
internal sealed record MetricTagModel(
    string TagName,
    string? Metric,
    string Access,
    bool ValueCanBeNull,
    string? GuardExpression = null,
    string? Root = null,
    bool RootIsOut = false)
{
    /// <summary>Whether the tag reads the result, so it can only be added after a call that returned.</summary>
    public bool ReadsResult => Root is null;

    /// <summary>Whether the tag goes on the instruments of <paramref name="metric"/>.</summary>
    public bool AppliesTo(MetricModel metric) =>
        Metric is null || string.Equals(Metric, metric.Metric, StringComparison.Ordinal);
}
