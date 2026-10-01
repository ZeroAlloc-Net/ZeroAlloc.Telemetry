using System.Diagnostics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// With no listener on the source and no instrument enabled, an awaitable method's proxy returns
/// the inner task as is (#169). Before, every proxy method was async, so a call whose inner method
/// had not completed allocated the proxy's state machine even with telemetry off.
/// </summary>
/// <remarks>
/// The inner methods return a task that is still pending, so the proxy cannot finish
/// synchronously: an async proxy would have to box its state machine. Allocations are counted on
/// the current thread only, and this class is the only user of its source and meter.
/// </remarks>
public sealed class NoListenerAllocationTests
{
    private const int Calls = 1_000;

    [Fact]
    public async Task Task_WithNoListener_AddsNoAllocation()
    {
        var inner = new PendingService();
        var proxy = new PendingServiceInstrumented(inner);
        var tasks = new Task[Calls];

        for (var i = 0; i < 100; i++)
            _ = proxy.RunAsync(i);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Calls; i++)
            tasks[i] = proxy.RunAsync(i);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
        tasks.Should().OnlyContain(t => !t.IsCompleted);
        inner.Complete(1);
        await Task.WhenAll(tasks).ConfigureAwait(true);
    }

    [Fact]
    public async Task ValueTask_WithNoListener_AddsNoAllocation()
    {
        var inner = new PendingService();
        var proxy = new PendingServiceInstrumented(inner);
        var tasks = new ValueTask[Calls];

        for (var i = 0; i < 100; i++)
            _ = proxy.StepAsync(i);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Calls; i++)
            tasks[i] = proxy.StepAsync(i);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
        inner.Complete(1);
        foreach (var task in tasks)
            await task.ConfigureAwait(true);
    }

    [Fact]
    public async Task ValueTaskOfT_WithNoListener_AddsNoAllocation_AndReturnsTheInnerResult()
    {
        var inner = new PendingService();
        var proxy = new PendingServiceInstrumented(inner);
        var tasks = new ValueTask<int>[Calls];

        for (var i = 0; i < 100; i++)
            _ = proxy.ReadAsync(i);

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < Calls; i++)
            tasks[i] = proxy.ReadAsync(i);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
        inner.Complete(42);
        foreach (var task in tasks)
            (await task.ConfigureAwait(true)).Should().Be(42);
    }

    /// <summary>
    /// The control: with a listener the same call goes through the async core, which allocates
    /// and records. Without it, a proxy that never instrumented anything would pass the tests above.
    /// </summary>
    [Fact]
    public async Task WithAListener_TheCallIsInstrumented()
    {
        using var capture = new MetricCapture(PendingService.SourceName);
        var spans = new List<string>();
        using var listener = new ActivityListener
        {
            ShouldListenTo = s => string.Equals(s.Name, PendingService.SourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a =>
            {
                lock (spans)
                    spans.Add(a.OperationName);
            },
        };
        ActivitySource.AddActivityListener(listener);

        var inner = new PendingService();
        var proxy = new PendingServiceInstrumented(inner);

        var before = GC.GetAllocatedBytesForCurrentThread();
        var read = proxy.ReadAsync(1);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().BePositive();
        inner.Complete(42);
        (await read.ConfigureAwait(true)).Should().Be(42);
        await proxy.RunAsync(2).ConfigureAwait(true);
        await proxy.StepAsync(3).ConfigureAwait(true);

        lock (spans)
            spans.Should().Equal("pending.valuetask_of_t", "pending.task", "pending.valuetask");
        capture.ValuesOf("pending.values").Should().Equal(42d);
        capture.ValuesOf("pending.task.calls").Should().Equal(1d);
        capture.ValuesOf("pending.valuetask.duration").Should().ContainSingle();
    }

    /// <summary>
    /// A metric listener alone, with no activity listener, still takes the instrumented path:
    /// the fast path needs every instrument of the method to be off.
    /// </summary>
    [Fact]
    public async Task WithOnlyAMeterListener_TheMetricIsRecorded()
    {
        using var capture = new MetricCapture(PendingService.SourceName);
        var inner = new PendingService();
        inner.Complete(7);
        var proxy = new PendingServiceInstrumented(inner);

        (await proxy.ReadAsync(1).ConfigureAwait(true)).Should().Be(7);

        capture.ValuesOf("pending.values").Should().Equal(7d);
    }
}
