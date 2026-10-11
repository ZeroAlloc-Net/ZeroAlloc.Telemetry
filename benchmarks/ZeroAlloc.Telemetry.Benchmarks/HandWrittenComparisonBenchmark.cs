using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using BenchmarkDotNet.Attributes;

namespace ZeroAlloc.Telemetry.Benchmarks;

// Compares ZA.Telemetry's generator-emitted instrumented proxy against
// hand-written ActivitySource / Meter wrapping — the alternative pattern
// developers reach for when they want trace spans + metrics without a
// source generator.
//
// Both rows exercise the "no listeners attached" hot path, the common
// production profile when tracing is sampled out. Activity.StartActivity
// returns null in that case; the counter increment is a single interlocked
// add. The question is whether ZA's generated code adds overhead vs the
// hand-rolled equivalent.
//
// The benchmark methods return the ValueTask itself rather than awaiting it in an async Task
// method, whose state machine and Task allocate 72 B per call and would hide what the proxy adds.
[MemoryDiagnoser]
[SimpleJob]
public class HandWrittenComparisonBenchmark
{
    private static readonly ActivitySource s_handSource = new("ZeroAlloc.Telemetry.Benchmarks.HandWritten");
    private static readonly Meter s_handMeter = new("ZeroAlloc.Telemetry.Benchmarks.HandWritten");
    private static readonly Counter<long> s_handCreated = s_handMeter.CreateCounter<long>("orders.created");
    private static readonly Histogram<double> s_handDuration = s_handMeter.CreateHistogram<double>("order.create_ms");

    private IOrderService _direct = null!;
    private IOrderService _zaProxy = null!;
    private HandWrittenOrderService _handWritten = null!;

    [GlobalSetup]
    public void Setup()
    {
        _direct = new OrderService();
        _zaProxy = new OrderServiceInstrumented(_direct);
        _handWritten = new HandWrittenOrderService(_direct);
    }

    [Benchmark(Baseline = true, Description = "Direct call (no instrumentation)")]
    [BenchmarkCategory("NoListeners")]
    public ValueTask<int> Direct()
        => _direct.CreateAsync("cust-1", CancellationToken.None);

    [Benchmark(Description = "Hand-written ActivitySource + Counter")]
    [BenchmarkCategory("NoListeners")]
    public ValueTask<int> HandWritten()
        => _handWritten.CreateAsync("cust-1", CancellationToken.None);

    [Benchmark(Description = "ZA.Telemetry generated proxy")]
    [BenchmarkCategory("NoListeners")]
    public ValueTask<int> Za_Proxy()
        => _zaProxy.CreateAsync("cust-1", CancellationToken.None);

    // The hand-written equivalent of what ZA.Telemetry's generator emits:
    // start an Activity, increment a counter, record a histogram, dispose.
    public sealed class HandWrittenOrderService : IOrderService
    {
        private readonly IOrderService _inner;
        public HandWrittenOrderService(IOrderService inner) => _inner = inner;

        public async ValueTask<int> CreateAsync(string customerId, CancellationToken ct)
        {
            using var activity = s_handSource.StartActivity("order.create");
            var start = Stopwatch.GetTimestamp();
            try
            {
                var result = await _inner.CreateAsync(customerId, ct).ConfigureAwait(false);
                s_handCreated.Add(1);
                return result;
            }
            finally
            {
                var elapsedMs = Stopwatch.GetElapsedTime(start).TotalMilliseconds;
                s_handDuration.Record(elapsedMs);
            }
        }
    }
}
