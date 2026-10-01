namespace ZeroAlloc.Telemetry;

/// <summary>
/// Records the elapsed time of each call, successful or failed, in a
/// <see cref="System.Diagnostics.Metrics.Histogram{T}"/> of <c>double</c>, in the time unit
/// <see cref="Unit"/> names: milliseconds by default.
/// </summary>
/// <remarks>
/// <para>
/// With <see cref="When"/> set, only non-throwing calls whose guard is true are recorded. A call
/// that throws has no result to evaluate the guard against, so a guarded histogram records
/// nothing for it. An unguarded histogram still records on both paths.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HistogramAttribute : Attribute
{
    /// <summary>The metric name passed to <see cref="System.Diagnostics.Metrics.Meter.CreateHistogram{T}(string, string?, string?)"/>.</summary>
    public string Metric { get; }

    /// <summary>
    /// A dotted path from the awaited return value to a <c>bool</c> or <c>bool?</c> member that
    /// must be true for the duration to be recorded, e.g. <c>IsSuccess</c>. Null records every
    /// call, including calls that throw.
    /// </summary>
    /// <remarks>
    /// Same semantics as <see cref="TraceTagFromResultAttribute.When"/>. The method must return a
    /// value; on <c>void</c>, <c>Task</c> or <c>ValueTask</c> the generator reports <c>ZTEL005</c>
    /// and the histogram records nothing, not even on the throw path.
    /// </remarks>
    public string? When { get; set; }

    /// <summary>
    /// The instrument's unit, which also decides what is recorded: <c>ms</c> records
    /// milliseconds, <c>s</c> seconds, <c>us</c> microseconds, <c>ns</c> nanoseconds,
    /// <c>min</c> minutes and <c>h</c> hours. Null records milliseconds and passes no unit.
    /// </summary>
    /// <remarks>
    /// Any other unit is reported as <c>ZTEL018</c>, and the duration is then recorded in
    /// milliseconds. The OpenTelemetry semantic conventions record durations in <c>s</c>.
    /// </remarks>
    public string? Unit { get; set; }

    /// <summary>
    /// Explicit bucket boundaries, passed to the instrument as
    /// <c>InstrumentAdvice&lt;double&gt;.HistogramBucketBoundaries</c>. Null leaves the choice to
    /// the listener.
    /// </summary>
    /// <remarks>
    /// The boundaries must be finite and strictly increasing, otherwise the generator reports
    /// <c>ZTEL019</c>. Advice needs System.Diagnostics.DiagnosticSource 9.0 or later; with an older
    /// one the generator reports <c>ZTEL020</c>. When several methods record one metric, the first
    /// that declares buckets sets them.
    /// </remarks>
    public double[]? Buckets { get; set; }

    /// <summary>The instrument's description. Null passes none.</summary>
    public string? Description { get; set; }

    /// <summary>Records call durations under <paramref name="metric"/>.</summary>
    /// <param name="metric">The metric name.</param>
    public HistogramAttribute(string metric) => Metric = metric;
}
