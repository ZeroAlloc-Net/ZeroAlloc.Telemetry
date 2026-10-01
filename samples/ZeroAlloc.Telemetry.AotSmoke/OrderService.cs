using System.Threading;
using System.Threading.Tasks;

namespace ZeroAlloc.Telemetry.AotSmoke;

public sealed class OrderService : IOrderService
{
    public int CallCount { get; private set; }

    public ValueTask<int> CreateAsync(string customerId, CancellationToken ct)
    {
        CallCount++;
        return ValueTask.FromResult(42);
    }

    public ValueTask<OrderReceipt> ReceiptAsync(string customerId, CancellationToken ct) =>
        ValueTask.FromResult(new OrderReceipt { Lines = 3, Region = "eu-west" });

    public ValueTask<OrderQuote> QuoteAsync(string customerId, CancellationToken ct) =>
        ValueTask.FromResult(new OrderQuote { Prices = new double[] { 9.5, 12.25 } });

    public ValueTask<OrderDecision> DecideAsync(string customerId, CancellationToken ct) =>
        ValueTask.FromResult(new OrderDecision { IsRejected = true, Reason = "out of stock" });

    public ValueTask<T> EchoAsync<T>(T value) where T : notnull => ValueTask.FromResult(value);
}
