using System.Diagnostics;
using System.Diagnostics.Metrics;
using ZeroAlloc.Telemetry;

namespace ZeroAlloc.Telemetry.Tests;

// This file verifies the proxy pattern that the generator emits works correctly at runtime.
// We use a hand-written proxy that matches generator output so tests don't depend on the generator running.
public class GeneratedProxyBehaviorTests
{
    [Instrument("TestSource")]
    private interface ISimpleService
    {
        [Trace("simple.do")]
        [Count("simple.calls")]
        ValueTask DoAsync(CancellationToken ct);

        [Trace("simple.get")]
        [Histogram("simple.get_ms")]
        ValueTask<string> GetAsync(CancellationToken ct);
    }

    // Hand-written proxy matching generator output pattern.
    // Pragmas suppress analyzer rules that the generator itself violates by design:
    //   EPC12 – SetStatus intentionally passes only the message (mirrors generated code)
    //   MA0004 – generated code does not use ConfigureAwait; proxy mirrors that
#pragma warning disable EPC12, MA0004
    private sealed class SimpleServiceInstrumented : ISimpleService
    {
        private static readonly ActivitySource _activitySource = new("TestSource");
        private static readonly Meter _meter = new("TestSource");
        private static readonly Counter<long> _simple_calls = _meter.CreateCounter<long>("simple.calls");
        private static readonly Histogram<double> _simple_get_ms = _meter.CreateHistogram<double>("simple.get_ms");

        private readonly ISimpleService _inner;
        public SimpleServiceInstrumented(ISimpleService inner) => _inner = inner;

        public async ValueTask DoAsync(CancellationToken ct)
        {
            using var _activity = _activitySource.StartActivity("simple.do");
            try
            {
                await _inner.DoAsync(ct);
                _simple_calls.Add(1);
            }
            catch (Exception _ex)
            {
                _activity?.SetStatus(ActivityStatusCode.Error, _ex.Message);
                throw;
            }
        }

        public async ValueTask<string> GetAsync(CancellationToken ct)
        {
            using var _activity = _activitySource.StartActivity("simple.get");
            var _sw = Stopwatch.GetTimestamp();
            try
            {
                var _result = await _inner.GetAsync(ct);
                _simple_get_ms.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);
                return _result;
            }
            catch (Exception _ex)
            {
                _activity?.SetStatus(ActivityStatusCode.Error, _ex.Message);
                _simple_get_ms.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);
                throw;
            }
        }
    }
#pragma warning restore EPC12, MA0004

    private sealed class FakeService : ISimpleService
    {
        public bool ShouldThrow { get; set; }

        public ValueTask DoAsync(CancellationToken ct) =>
            ShouldThrow
                ? ValueTask.FromException(new InvalidOperationException("boom"))
                : ValueTask.CompletedTask;

        public ValueTask<string> GetAsync(CancellationToken ct) =>
            ShouldThrow
                ? ValueTask.FromException<string>(new InvalidOperationException("boom"))
                : ValueTask.FromResult("result");
    }

    [Fact]
    public async Task DoAsync_StartsAndStopsActivity()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => string.Equals(src.Name, "TestSource", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a => activities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        var proxy = new SimpleServiceInstrumented(new FakeService());
        await proxy.DoAsync(default).ConfigureAwait(true);

        activities.Should().ContainSingle(a => string.Equals(a.OperationName, "simple.do", StringComparison.Ordinal));
        activities[0].Status.Should().Be(ActivityStatusCode.Unset);
    }

    [Fact]
    public async Task DoAsync_SetsErrorStatus_OnException()
    {
        var activities = new List<Activity>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = src => string.Equals(src.Name, "TestSource", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
            ActivityStopped = a => activities.Add(a)
        };
        ActivitySource.AddActivityListener(listener);

        var proxy = new SimpleServiceInstrumented(new FakeService { ShouldThrow = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.DoAsync(default).AsTask()).ConfigureAwait(true);

        activities.Should().ContainSingle(a => string.Equals(a.OperationName, "simple.do", StringComparison.Ordinal));
        activities[0].Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public async Task DoAsync_IncrementsCounter_OnSuccess()
    {
        long count = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (string.Equals(instrument.Name, "simple.calls", StringComparison.Ordinal)) ml.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => count += value);
        listener.Start();

        var proxy = new SimpleServiceInstrumented(new FakeService());
        await proxy.DoAsync(default).ConfigureAwait(true);

        count.Should().Be(1);
    }

    [Fact]
    public async Task DoAsync_DoesNotIncrementCounter_OnException()
    {
        long count = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (string.Equals(instrument.Name, "simple.calls", StringComparison.Ordinal)) ml.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<long>((_, value, _, _) => count += value);
        listener.Start();

        var proxy = new SimpleServiceInstrumented(new FakeService { ShouldThrow = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.DoAsync(default).AsTask()).ConfigureAwait(true);

        count.Should().Be(0);
    }

    [Fact]
    public async Task GetAsync_RecordsHistogram_OnSuccess()
    {
        double? recorded = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (string.Equals(instrument.Name, "simple.get_ms", StringComparison.Ordinal)) ml.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => recorded = value);
        listener.Start();

        var proxy = new SimpleServiceInstrumented(new FakeService());
        await proxy.GetAsync(default).ConfigureAwait(true);

        double recordedValue = recorded ?? throw new InvalidOperationException("No histogram measurement recorded");
        recordedValue.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task GetAsync_RecordsHistogram_OnException()
    {
        double? recorded = null;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, ml) =>
        {
            if (string.Equals(instrument.Name, "simple.get_ms", StringComparison.Ordinal)) ml.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, value, _, _) => recorded = value);
        listener.Start();

        var proxy = new SimpleServiceInstrumented(new FakeService { ShouldThrow = true });
        await Assert.ThrowsAsync<InvalidOperationException>(() => proxy.GetAsync(default).AsTask()).ConfigureAwait(true);

        double recordedValue = recorded ?? throw new InvalidOperationException("No histogram measurement recorded");
        recordedValue.Should().BeGreaterThanOrEqualTo(0);
    }
}
