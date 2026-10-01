using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.Telemetry;

namespace ZeroAlloc.Telemetry.AotSmoke;

[Instrument("ZeroAlloc.Telemetry.AotSmoke")]
public interface IOrderService
{
    [Trace("order.create")]
    [Count("orders.created")]
    [Histogram("order.create_ms")]
    ValueTask<int> CreateAsync(string customerId, CancellationToken ct);

    [CountFromResult("orders.lines", "Lines", Unit = "{line}")]
    [MetricTagFromResult("order.region", "Region")]
    ValueTask<OrderReceipt> ReceiptAsync(string customerId, CancellationToken ct);

    // Metric conventions (#171): a duration in seconds with bucket advice, a histogram per
    // element of the result, and constant and parameter tags.
    [Histogram("order.quote.duration", Unit = "s", Buckets = new[] { 0.001, 0.01, 0.1, 1d })]
    [HistogramFromResult("order.quote.prices", "Prices", Each = true)]
    [MetricTagConstant("order.operation", "quote")]
    ValueTask<OrderQuote> QuoteAsync([MetricTag("order.customer")] string customerId, CancellationToken ct);

    // A generic method: the proxy repeats its type parameter and constraint (#168).
    [Trace("order.echo")]
    ValueTask<T> EchoAsync<T>(T value) where T : notnull;
}
