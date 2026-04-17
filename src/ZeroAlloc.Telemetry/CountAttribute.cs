namespace ZeroAlloc.Telemetry;

/// <summary>
/// Increments a <see cref="System.Diagnostics.Metrics.Counter{T}"/> of <c>long</c>
/// by 1 after a successful (non-throwing) call.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CountAttribute : Attribute
{
    /// <summary>The metric name passed to <see cref="System.Diagnostics.Metrics.Meter.CreateCounter{T}"/>.</summary>
    public string Metric { get; }

    public CountAttribute(string metric) => Metric = metric;
}
