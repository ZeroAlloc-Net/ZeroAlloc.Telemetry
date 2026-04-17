namespace ZeroAlloc.Telemetry;

/// <summary>
/// Marks an interface for source-generated instrumentation.
/// The generator emits a proxy class <c>XxxInstrumented</c> that implements the same interface,
/// wraps an inner instance, and records <see cref="System.Diagnostics.Activity"/> spans
/// and <see cref="System.Diagnostics.Metrics.Meter"/> instruments per method.
/// </summary>
/// <example>
/// <code>
/// [Instrument(ActivitySource = "MyApp.Orders")]
/// public interface IOrderService
/// {
///     [Trace(Name = "order.create")]
///     [Count(Metric = "orders.created")]
///     ValueTask&lt;OrderId&gt; CreateOrderAsync(CreateOrderRequest request, CancellationToken ct);
/// }
/// // Generator emits: OrderServiceInstrumented : IOrderService
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Interface)]
public sealed class InstrumentAttribute : Attribute
{
    /// <summary>The <see cref="System.Diagnostics.ActivitySource"/> name for this type.</summary>
    public string ActivitySource { get; }

    public InstrumentAttribute(string activitySource) => ActivitySource = activitySource;
}
