using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
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

    // Span conventions (#170): kind, a name from a parameter, error status from the result, and
    // tags passed to the sampler.
    [Trace("decide {customerId}", Kind = ActivityKind.Client, ErrorWhen = "IsRejected", ErrorDescription = "Reason", TagsAtStart = true)]
    [TraceTagConstant("order.channel", "web")]
    ValueTask<OrderDecision> DecideAsync(
        [TraceTag("order.customer")] [TraceTag("order.customer.length", "Length")] string customerId,
        CancellationToken ct);

    // Member shapes (#173): an out parameter, a span on an awaitable method, and a property.
    [Trace("order.find")]
    bool TryFind(string id, [NotNullWhen(true)] out OrderReceipt? receipt);

    [Trace("order.parse")]
    ValueTask<int> ParseAsync(ReadOnlySpan<char> text, out int consumed);

    int Pending { get; }

    // The exception path (#184): error.type on the span and the duration point, and the
    // exception's message kept out of the span.
    [Trace("order.cancel", ExceptionDescription = false)]
    [Histogram("order.cancel_ms")]
    ValueTask CancelAsync(string orderId);

    // A generic method: the proxy repeats its type parameter and constraint (#168).
    [Trace("order.echo")]
    ValueTask<T> EchoAsync<T>(T value) where T : notnull;
}
