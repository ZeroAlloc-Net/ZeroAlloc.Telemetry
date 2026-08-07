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

    /// <summary>
    /// Emits the proxy as <c>public</c> instead of the default <c>internal</c>.
    /// </summary>
    /// <remarks>
    /// An internal proxy can only be constructed from the assembly declaring the interface. Where
    /// the interface lives in a shared abstractions assembly and is implemented across several
    /// packages, those packages cannot wrap their own implementations — which is also the assembly
    /// where a new dependency is least welcome. Setting this to <see langword="true"/> makes the
    /// proxy constructible from any referencing assembly.
    /// <para>
    /// This widens the declaring assembly's public API surface, so it is opt-in: the default stays
    /// <c>internal</c>.
    /// </para>
    /// </remarks>
    public bool PublicProxy { get; set; }

    /// <summary>Marks the interface for instrumentation against the named activity source.</summary>
    /// <param name="activitySource">The activity source name, typically the service or module name.</param>
    public InstrumentAttribute(string activitySource) => ActivitySource = activitySource;
}
