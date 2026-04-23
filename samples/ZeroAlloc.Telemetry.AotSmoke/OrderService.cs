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
}
