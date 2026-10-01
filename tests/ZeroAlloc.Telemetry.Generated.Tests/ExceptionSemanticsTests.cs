using System.Diagnostics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// An awaitable proxy method fails the same way with and without a listener (#169). Without one
/// it returns the inner task directly, but an exception the inner method throws before returning
/// a task still comes back inside the returned task, as it does from the async core: faulted, or
/// canceled for an <see cref="OperationCanceledException"/>, with the original exception.
/// </summary>
/// <remarks>
/// A caller that stores the task and wraps only the await in try/catch must not see the
/// difference between telemetry on and off. The class is the only user of its source and meter.
/// </remarks>
public sealed class ExceptionSemanticsTests
{
    public static TheoryData<Failure, bool> Cases()
    {
        var data = new TheoryData<Failure, bool>();
        foreach (var failure in Enum.GetValues<Failure>())
        {
            data.Add(failure, false);
            data.Add(failure, true);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Task_FailsInsideTheReturnedTask(Failure failure, bool listening)
    {
        var (proxy, exception) = Create(failure);
        using var listener = listening ? Listen() : null;

        var task = proxy.RunAsync();

        await AssertFails(task, failure, exception).ConfigureAwait(true);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ValueTask_FailsInsideTheReturnedTask(Failure failure, bool listening)
    {
        var (proxy, exception) = Create(failure);
        using var listener = listening ? Listen() : null;

        var task = proxy.StepAsync();

        await AssertFails(task.AsTask(), failure, exception).ConfigureAwait(true);
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task ValueTaskOfT_FailsInsideTheReturnedTask(Failure failure, bool listening)
    {
        var (proxy, exception) = Create(failure);
        using var listener = listening ? Listen() : null;

        var task = proxy.ReadAsync();

        await AssertFails(task.AsTask(), failure, exception).ConfigureAwait(true);
    }

    /// <summary>A method without instruments forwards the same way, so it fails the same way too.</summary>
    [Theory]
    [MemberData(nameof(Cases))]
    public async Task MethodWithoutInstruments_FailsInsideTheReturnedTask(Failure failure, bool listening)
    {
        var (proxy, exception) = Create(failure);
        using var listener = listening ? Listen() : null;

        var task = proxy.PlainAsync();

        await AssertFails(task, failure, exception).ConfigureAwait(true);
    }

    private static (IThrowingService Proxy, Exception Exception) Create(Failure failure)
    {
        Exception exception = failure == Failure.SynchronousCancel
            ? new OperationCanceledException(new CancellationToken(canceled: true))
            : new InvalidOperationException("boom");

        var proxy = new ThrowingServiceInstrumented(new ThrowingService { Failure = failure, Exception = exception });
        return (proxy, exception);
    }

    private static async Task AssertFails(Task task, Failure failure, Exception exception)
    {
        var thrown = await Record(task).ConfigureAwait(true);

        thrown.Should().BeSameAs(exception);
        if (failure == Failure.SynchronousCancel)
            task.IsCanceled.Should().BeTrue();
        else
            task.IsFaulted.Should().BeTrue();
    }

    private static async Task<Exception?> Record(Task task)
    {
        try
        {
            await task.ConfigureAwait(true);
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }

    /// <summary>Enables the source and every instrument of the meter, so each call takes the async core.</summary>
    private static Listening Listen() => new();

    private sealed class Listening : IDisposable
    {
        private readonly ActivityListener _activities = new()
        {
            ShouldListenTo = s => string.Equals(s.Name, ThrowingService.SourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
        };

        private readonly MetricCapture _metrics = new(ThrowingService.SourceName);

        public Listening() => ActivitySource.AddActivityListener(_activities);

        public void Dispose()
        {
            _activities.Dispose();
            _metrics.Dispose();
        }
    }
}
