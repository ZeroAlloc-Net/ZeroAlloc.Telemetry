using System.Diagnostics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// A call the caller cancelled through the method's own token is not an error: its span stays
/// Unset and is tagged <c>cancelled</c>, and its duration point has no <c>error.type</c> (#233).
/// Any other cancellation, and every one with <c>CancellationIsError = true</c>, is an error.
/// </summary>
/// <remarks>The class is the only user of its source and meter.</remarks>
public sealed class CancellationTelemetryTests
{
    private const string ErrorType = "System.OperationCanceledException";

    [Fact]
    public async Task ACallerCancellation_LeavesTheSpanUnset_AndTheDurationPointWithoutErrorType()
    {
        using var spans = new Spans();
        using var metrics = new MetricCapture(CancellableOperations.SourceName);
        var proxy = new CancellableOperationsInstrumented(new CancellableOperations());

        var call = async () => await proxy.RunAsync(new CancellationToken(canceled: true)).ConfigureAwait(false);

        await call.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(true);
        var span = spans.Stopped.Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Unset);
        span.GetTagItem("cancelled").Should().Be(true);
        span.GetTagItem("error.type").Should().BeNull();
        metrics.TagsOf("cancellable.run.duration").Should().ContainSingle().Which.Should().BeEmpty();
    }

    [Fact]
    public async Task ACancellationTheCallerDidNotAskFor_IsAnError()
    {
        using var spans = new Spans();
        using var metrics = new MetricCapture(CancellableOperations.SourceName);
        var proxy = new CancellableOperationsInstrumented(new CancellableOperations());

        var call = async () => await proxy.RunAsync(CancellationToken.None).ConfigureAwait(false);

        await call.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(true);
        var span = spans.Stopped.Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.GetTagItem("error.type").Should().Be(ErrorType);
        span.GetTagItem("cancelled").Should().BeNull();
        metrics.TagsOf("cancellable.run.duration").Should().ContainSingle()
            .Which.Should().Equal(new Dictionary<string, object?>(StringComparer.Ordinal) { ["error.type"] = ErrorType });
    }

    [Fact]
    public async Task CancellationIsErrorTrue_RecordsTheCallersCancellationAsAnError()
    {
        using var spans = new Spans();
        using var metrics = new MetricCapture(CancellableOperations.SourceName);
        var proxy = new CancellableOperationsInstrumented(new CancellableOperations());

        var call = () => proxy.StrictAsync(new CancellationToken(canceled: true));

        await call.Should().ThrowAsync<OperationCanceledException>().ConfigureAwait(true);
        var span = spans.Stopped.Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.GetTagItem("error.type").Should().Be(ErrorType);
        span.GetTagItem("cancelled").Should().BeNull();
        metrics.TagsOf("cancellable.strict.duration").Should().ContainSingle()
            .Which.Should().Equal(new Dictionary<string, object?>(StringComparer.Ordinal) { ["error.type"] = ErrorType });
    }

    [Fact]
    public void ASynchronousCallerCancellation_LeavesTheDurationPointWithoutErrorType()
    {
        using var metrics = new MetricCapture(CancellableOperations.SourceName);
        var proxy = new CancellableOperationsInstrumented(new CancellableOperations());

        var call = () => proxy.Run(new CancellationToken(canceled: true));

        call.Should().Throw<OperationCanceledException>();
        metrics.TagsOf("cancellable.sync.duration").Should().ContainSingle().Which.Should().BeEmpty();
    }

    [Fact]
    public void ACallerCancellationBeforeTheAwaitable_EndsTheSpanUnset()
    {
        using var spans = new Spans();
        var proxy = new CancellableOperationsInstrumented(new CancellableOperations());

        // The split call rethrows synchronously, as there is no async method to carry the exception.
        Action call = () => _ = proxy.SplitAsync(1, new CancellationToken(canceled: true));

        call.Should().Throw<OperationCanceledException>();
        var span = spans.Stopped.Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Unset);
        span.GetTagItem("cancelled").Should().Be(true);
        span.GetTagItem("error.type").Should().BeNull();
    }

    private sealed class Spans : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly Lock _gate = new();
        private readonly List<Activity> _stopped = [];

        public Spans()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = s => string.Equals(s.Name, CancellableOperations.SourceName, StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = a =>
                {
                    lock (_gate)
                        _stopped.Add(a);
                },
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public IReadOnlyList<Activity> Stopped
        {
            get
            {
                lock (_gate)
                    return [.. _stopped];
            }
        }

        public void Dispose() => _listener.Dispose();
    }
}
