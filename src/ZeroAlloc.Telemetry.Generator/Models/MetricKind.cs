namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>The instrument a metric is recorded on, which is also the type of its field.</summary>
internal enum MetricKind
{
    /// <summary><c>Counter&lt;long&gt;</c>.</summary>
    Counter,

    /// <summary><c>Histogram&lt;double&gt;</c>.</summary>
    Histogram,
}
