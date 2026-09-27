namespace ZeroAlloc.Telemetry;

/// <summary>
/// Records a member of the return value in a
/// <see cref="System.Diagnostics.Metrics.Histogram{T}"/> of <c>double</c> after a successful
/// (non-throwing) call.
/// </summary>
/// <remarks>
/// <para>
/// For distributions only known once the call returns, such as a confidence score, a result size
/// or a cost. The path is read from the awaited return value, exactly as
/// <see cref="TraceTagFromResultAttribute"/> reads it.
/// </para>
/// <para>
/// The member must be numeric: <c>sbyte</c>, <c>byte</c>, <c>short</c>, <c>ushort</c>, <c>int</c>,
/// <c>uint</c>, <c>long</c>, <c>ulong</c>, <c>float</c>, <c>double</c> or <c>decimal</c>, or a
/// nullable form of one. Anything else is reported as <c>ZTEL009</c>. A <c>decimal</c> is
/// converted with an explicit cast to <c>double</c>. A null value, or a null anywhere along the
/// path, is not recorded.
/// </para>
/// <para>
/// A call that throws has no result, so nothing is recorded for it. Does not require
/// <see cref="TraceAttribute"/>. May be applied more than once.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [HistogramFromResult("answer.confidence", "Value.Confidence", When = "IsSuccess", Unit = "1")]
/// ValueTask&lt;Result&lt;Answer, AskError&gt;&gt; AskAsync(Question question, CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class HistogramFromResultAttribute : Attribute
{
    /// <summary>The metric name passed to <see cref="System.Diagnostics.Metrics.Meter.CreateHistogram{T}(string, string?, string?)"/>.</summary>
    public string Metric { get; }

    /// <summary>
    /// Dotted path from the awaited return value to the member to record, e.g. <c>Value.Score</c>.
    /// Empty records the return value itself.
    /// </summary>
    public string Member { get; }

    /// <summary>
    /// A dotted path to a <c>bool</c> or <c>bool?</c> member that must be true for the value to be
    /// recorded, e.g. <c>IsSuccess</c>. <see cref="Member"/> is not read otherwise. Null always records.
    /// </summary>
    public string? When { get; set; }

    /// <summary>The instrument's unit, e.g. <c>1</c> for a ratio. Null passes no unit.</summary>
    public string? Unit { get; set; }

    /// <summary>The instrument's description. Null passes none.</summary>
    public string? Description { get; set; }

    /// <summary>Records <paramref name="member"/> of the return value in <paramref name="metric"/>.</summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="member">Dotted path from the awaited return value.</param>
    public HistogramFromResultAttribute(string metric, string member)
    {
        Metric = metric;
        Member = member;
    }
}
