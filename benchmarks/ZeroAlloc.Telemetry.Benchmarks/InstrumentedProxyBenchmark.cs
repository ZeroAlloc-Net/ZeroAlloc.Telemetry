using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;
using ZeroAlloc.Telemetry;

namespace ZeroAlloc.Telemetry.Benchmarks;

// Measures the per-call overhead of the generator-emitted {Interface}Instrumented
// proxy when no listeners are attached. The claim: the Activity/Meter wrappers
// short-circuit cheaply when no subscribers are listening — ActivitySource.StartActivity
// returns null, the Activity.Dispose path is a no-op, and the Meter counter increment
// is a single interlocked add.
//
// The "no-listeners" path is the common production profile when tracing is sampled
// away. Baseline: direct call on the underlying service.
[MemoryDiagnoser]
[SimpleJob]
public class InstrumentedProxyBenchmark
{
    private IOrderService _direct = null!;
    private IOrderService _proxy = null!;

    [GlobalSetup]
    public void Setup()
    {
        _direct = new OrderService();
        _proxy = new OrderServiceInstrumented(_direct);
    }

    [Benchmark(Baseline = true, Description = "direct")]
    public async Task<int> Direct()
        => await _direct.CreateAsync("cust-1", CancellationToken.None).ConfigureAwait(false);

    [Benchmark(Description = "proxy (no listeners)")]
    public async Task<int> Proxied()
        => await _proxy.CreateAsync("cust-1", CancellationToken.None).ConfigureAwait(false);
}

[Instrument("ZeroAlloc.Telemetry.Benchmarks")]
public interface IOrderService
{
    [Trace("order.create")]
    [Count("orders.created")]
    [Histogram("order.create_ms")]
    ValueTask<int> CreateAsync(string customerId, CancellationToken ct);
}

public sealed class OrderService : IOrderService
{
    public ValueTask<int> CreateAsync(string customerId, CancellationToken ct)
        => ValueTask.FromResult(42);
}
