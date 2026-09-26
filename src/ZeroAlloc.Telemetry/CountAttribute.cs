namespace ZeroAlloc.Telemetry;

/// <summary>
/// Increments a <see cref="System.Diagnostics.Metrics.Counter{T}"/> of <c>long</c>
/// by 1 after a successful (non-throwing) call.
/// </summary>
/// <remarks>
/// <para>
/// A method returning a <c>Result&lt;T, E&gt;</c> returns normally when it fails, so without a
/// guard a failed Result is counted as a success. Set <see cref="When"/> to count only the calls
/// whose result says they succeeded.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Count("orders.accepted", When = "IsSuccess", Unit = "{order}")]
/// ValueTask&lt;Result&lt;OrderId, OrderError&gt;&gt; AcceptAsync(Order order, CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CountAttribute : Attribute
{
    /// <summary>The metric name passed to <see cref="System.Diagnostics.Metrics.Meter.CreateCounter{T}(string, string?, string?)"/>.</summary>
    public string Metric { get; }

    /// <summary>
    /// A dotted path from the awaited return value to a <c>bool</c> or <c>bool?</c> member that
    /// must be true for the call to be counted, e.g. <c>IsSuccess</c>. Null counts every
    /// non-throwing call.
    /// </summary>
    /// <remarks>
    /// Same semantics as <see cref="TraceTagFromResultAttribute.When"/>. The method must return a
    /// value; on <c>void</c>, <c>Task</c> or <c>ValueTask</c> the generator reports <c>ZTEL005</c>
    /// and the counter records nothing.
    /// </remarks>
    public string? When { get; set; }

    /// <summary>The instrument's unit, e.g. <c>{order}</c>. Null passes no unit.</summary>
    public string? Unit { get; set; }

    /// <summary>The instrument's description. Null passes none.</summary>
    public string? Description { get; set; }

    /// <summary>Counts calls under <paramref name="metric"/>.</summary>
    /// <param name="metric">The metric name.</param>
    public CountAttribute(string metric) => Metric = metric;
}
