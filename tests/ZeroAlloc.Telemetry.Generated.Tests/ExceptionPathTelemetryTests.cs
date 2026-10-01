using System.Diagnostics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// A call that throws is classified by <c>error.type</c> on its span and its duration point, and
/// its span takes the exception's message only when the method allows it (#184).
/// </summary>
/// <remarks>The class is the only user of its source and meter.</remarks>
public sealed class ExceptionPathTelemetryTests
{
    private const string ErrorType = "System.InvalidOperationException";

    [Fact]
    public async Task AThrownCall_SetsErrorType_OnTheSpanAndTheDurationPoint_WithTheMessage()
    {
        using var spans = new Spans();
        using var metrics = new MetricCapture(FailingOperations.SourceName);
        var proxy = new FailingOperationsInstrumented(new FailingOperations());

        var call = () => proxy.DescribedAsync();

        await call.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
        var span = spans.Stopped.Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.StatusDescription.Should().Be(FailingOperations.Message);
        span.GetTagItem("error.type").Should().Be(ErrorType);
        metrics.TagsOf("failing.described.duration").Should().ContainSingle()
            .Which.Should().Equal(new Dictionary<string, object?>(StringComparer.Ordinal) { ["error.type"] = ErrorType });
    }

    [Fact]
    public async Task ExceptionDescriptionFalse_KeepsTheMessageOutOfTheSpan()
    {
        using var spans = new Spans();
        var proxy = new FailingOperationsInstrumented(new FailingOperations());

        var call = () => proxy.QuietAsync();

        await call.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);
        var span = spans.Stopped.Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.StatusDescription.Should().BeNull();
        span.GetTagItem("error.type").Should().Be(ErrorType);
        span.TagObjects.Select(t => t.Value).OfType<string>().Should().NotContain(v => v.Contains("secret", StringComparison.Ordinal));
    }

    [Fact]
    public void AThrownCall_WithoutASpan_StillTagsTheDurationPoint()
    {
        using var metrics = new MetricCapture(FailingOperations.SourceName);
        var proxy = new FailingOperationsInstrumented(new FailingOperations());

        var call = () => proxy.Untraced();

        call.Should().Throw<InvalidOperationException>();
        metrics.TagsOf("failing.untraced.duration").Should().ContainSingle()
            .Which.Should().Equal(new Dictionary<string, object?>(StringComparer.Ordinal) { ["error.type"] = ErrorType });
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
                ShouldListenTo = s => string.Equals(s.Name, FailingOperations.SourceName, StringComparison.Ordinal),
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
