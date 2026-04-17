namespace ZeroAlloc.Telemetry;

/// <summary>
/// Records the elapsed time in milliseconds in a
/// <see cref="System.Diagnostics.Metrics.Histogram{T}"/> of <c>double</c>
/// after each call (successful or failed).
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HistogramAttribute : Attribute
{
    /// <summary>The metric name passed to <see cref="System.Diagnostics.Metrics.Meter.CreateHistogram{T}"/>.</summary>
    public string Metric { get; }

    public HistogramAttribute(string metric) => Metric = metric;
}
