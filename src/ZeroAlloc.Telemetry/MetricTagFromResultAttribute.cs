namespace ZeroAlloc.Telemetry;

/// <summary>
/// Adds a member of the return value as a tag, a dimension, on the metrics the method records.
/// </summary>
/// <remarks>
/// <para>
/// For dimensions only known once the call returns, such as the versioned model id a service
/// answered with. The path is read from the awaited return value, exactly as
/// <see cref="TraceTagFromResultAttribute"/> reads it. On a <c>Result&lt;T, E&gt;</c>, the
/// success value is reached through <c>Value</c>, as in <c>Value.Model</c>.
/// </para>
/// <para>
/// The tag goes on every measurement the method records after a successful, non-throwing call:
/// <see cref="CountAttribute"/>, <see cref="HistogramAttribute"/>,
/// <see cref="CountFromResultAttribute"/> and <see cref="HistogramFromResultAttribute"/>. Set
/// <see cref="Metric"/> to restrict it to the instruments of one metric name. An unguarded
/// <see cref="HistogramAttribute"/> also records when the call throws; there is no result then,
/// so that measurement carries no result tags.
/// </para>
/// <para>
/// The tags are only built for an instrument that a listener has enabled, so a call with no
/// listener neither reads the member nor boxes its value. A null value, or a null anywhere along
/// the path, adds no tag; the measurement is still recorded. When <see cref="When"/> is false the
/// member is not read and no tag is added.
/// </para>
/// <para>
/// May be applied more than once; the tags combine. Two tags with the same name on one metric
/// are reported as <c>ZTEL012</c>. A <see cref="Metric"/> that names no metric the method
/// records, or a method that records no metric at all, is reported as <c>ZTEL011</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [CountFromResult("llm.tokens.input", "Value.Usage.InputTokens", When = "IsSuccess")]
/// [HistogramFromResult("llm.cost", "Value.Cost", When = "IsSuccess")]
/// [MetricTagFromResult("gen_ai.response.model", "Value.Model", When = "IsSuccess")]
/// [MetricTagFromResult("llm.cached", "Value.Cached", When = "IsSuccess", Metric = "llm.cost")]
/// ValueTask&lt;Result&lt;ChatResponse, ChatError&gt;&gt; CompleteAsync(ChatRequest request, CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MetricTagFromResultAttribute : Attribute
{
    /// <summary>The tag key added to each measurement.</summary>
    public string Name { get; }

    /// <summary>
    /// Dotted path from the awaited return value to the member to record, e.g. <c>Value.Model</c>.
    /// Empty records the return value itself.
    /// </summary>
    public string Member { get; }

    /// <summary>
    /// A dotted path to a <c>bool</c> or <c>bool?</c> member that must be true for the tag to be
    /// added, e.g. <c>IsSuccess</c>. <see cref="Member"/> is not read otherwise. Null always adds.
    /// </summary>
    public string? When { get; set; }

    /// <summary>
    /// The metric name to restrict the tag to. Null adds it to every metric the method records.
    /// </summary>
    public string? Metric { get; set; }

    /// <summary>Adds <paramref name="member"/> of the return value as the tag <paramref name="name"/>.</summary>
    /// <param name="name">The tag key.</param>
    /// <param name="member">Dotted path from the awaited return value.</param>
    public MetricTagFromResultAttribute(string name, string member)
    {
        Name = name;
        Member = member;
    }
}
