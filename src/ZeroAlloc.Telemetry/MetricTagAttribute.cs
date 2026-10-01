namespace ZeroAlloc.Telemetry;

/// <summary>
/// Adds an argument, or a member of it, as a tag on the metrics the method records.
/// </summary>
/// <remarks>
/// <para>
/// The metric counterpart of <see cref="TraceTagAttribute"/>, for dimensions known before the
/// call, such as the requested model. The tag goes on every measurement the method records:
/// <see cref="CountAttribute"/>, <see cref="HistogramAttribute"/>,
/// <see cref="CountFromResultAttribute"/> and <see cref="HistogramFromResultAttribute"/>, including
/// the measurement an unguarded <see cref="HistogramAttribute"/> records when the call throws. Set
/// <see cref="Metric"/> to restrict it to the instruments of one metric name.
/// </para>
/// <para>
/// The tags are only built for an instrument that a listener has enabled, so a call with no
/// listener neither reads the argument nor boxes it. A null value, or a null anywhere along the
/// path, adds no tag. A path that names no member is reported as <c>ZTEL010</c> and adds no tag.
/// </para>
/// <para>
/// A <see cref="Metric"/> that names no metric the method records, or a method that records no
/// metric, is reported as <c>ZTEL011</c>. Two tags with one name on one metric are <c>ZTEL012</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Histogram("gen_ai.client.operation.duration", Unit = "s")]
/// ValueTask&lt;ChatResponse&gt; CompleteAsync(
///     [MetricTag("gen_ai.request.model", "Model")] ChatRequest request,
///     CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class MetricTagAttribute : Attribute
{
    /// <summary>The tag key added to each measurement.</summary>
    public string Name { get; }

    /// <summary>
    /// Dotted path from the argument to the member to record, e.g. <c>Model</c>. Null records the
    /// argument itself.
    /// </summary>
    public string? Member { get; }

    /// <summary>
    /// The metric name to restrict the tag to. Null adds it to every metric the method records.
    /// </summary>
    public string? Metric { get; set; }

    /// <summary>Adds the decorated argument as the tag <paramref name="name"/>.</summary>
    /// <param name="name">The tag key, e.g. <c>gen_ai.operation.name</c>.</param>
    public MetricTagAttribute(string name)
    {
        Name = name;
        Member = null;
    }

    /// <summary>Adds <paramref name="member"/> of the decorated argument as the tag <paramref name="name"/>.</summary>
    /// <param name="name">The tag key, e.g. <c>gen_ai.request.model</c>.</param>
    /// <param name="member">Dotted path from the argument, e.g. <c>Options.Model</c>.</param>
    public MetricTagAttribute(string name, string member)
    {
        Name = name;
        Member = member;
    }
}
