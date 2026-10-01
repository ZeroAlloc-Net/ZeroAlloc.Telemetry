namespace ZeroAlloc.Telemetry;

/// <summary>
/// Adds a constant tag to the metrics the method records.
/// </summary>
/// <remarks>
/// <para>
/// The metric counterpart of <see cref="TraceTagConstantAttribute"/>, for dimensions fixed by the
/// method, such as <c>gen_ai.operation.name</c> or <c>gen_ai.provider.name</c>. The tag goes on
/// every measurement the method records, including the measurement an unguarded
/// <see cref="HistogramAttribute"/> records when the call throws. Set <see cref="Metric"/> to
/// restrict it to the instruments of one metric name.
/// </para>
/// <para>
/// The value must be a constant an attribute can carry: a string, bool, char, number or enum. A
/// null adds no tag. An array or a type is reported as <c>ZTEL021</c>. The tags are only built
/// for an instrument that a listener has enabled.
/// </para>
/// <para>
/// May be applied more than once. A <see cref="Metric"/> that names no metric the method records,
/// or a method that records no metric, is reported as <c>ZTEL011</c>. Two tags with one name on
/// one metric are <c>ZTEL012</c>.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Histogram("gen_ai.client.operation.duration", Unit = "s")]
/// [MetricTagConstant("gen_ai.operation.name", "chat")]
/// [MetricTagConstant("gen_ai.provider.name", "openai")]
/// ValueTask&lt;ChatResponse&gt; CompleteAsync(ChatRequest request, CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MetricTagConstantAttribute : Attribute
{
    /// <summary>The tag key added to each measurement.</summary>
    public string Name { get; }

    /// <summary>The constant value recorded under <see cref="Name"/>.</summary>
    public object? Value { get; }

    /// <summary>
    /// The metric name to restrict the tag to. Null adds it to every metric the method records.
    /// </summary>
    public string? Metric { get; set; }

    /// <summary>Adds <paramref name="value"/> as the tag <paramref name="name"/>.</summary>
    /// <param name="name">The tag key, e.g. <c>gen_ai.operation.name</c>.</param>
    /// <param name="value">A constant: string, bool, char, numeric or enum.</param>
    public MetricTagConstantAttribute(string name, object? value)
    {
        Name = name;
        Value = value;
    }
}
