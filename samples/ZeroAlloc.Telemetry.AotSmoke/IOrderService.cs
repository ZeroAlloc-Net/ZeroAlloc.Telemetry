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
}
