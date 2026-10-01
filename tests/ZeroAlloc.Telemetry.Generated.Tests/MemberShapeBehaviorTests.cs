using System.Diagnostics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Members that used to make the proxy fail to compile (#173), forwarded and instrumented by the
/// real generator: parameter modifiers, a ref struct on an awaitable method, properties, an
/// indexer, an event and an inherited member.
/// </summary>
/// <remarks>The class is the only user of its source and meter, so its tests run sequentially and in isolation.</remarks>
public sealed class MemberShapeBehaviorTests
{
    [Fact]
    public void OutAndRefParameters_AreForwarded_AndInstrumented()
    {
        using var spans = new Spans();
        using var metrics = new MetricCapture(ShapeService.SourceName);
        IShapeService proxy = new ShapeServiceInstrumented(new ShapeService());

        proxy.TryGet("known", out var value).Should().BeTrue();
        value.Should().Be("found");
        proxy.TryGet("other", out var missing).Should().BeFalse();
        missing.Should().BeNull();

        var bumped = 1;
        proxy.Bump(ref bumped, 2, 3);
        bumped.Should().Be(6);

        spans.Names.Should().Equal("shape.tryget", "shape.tryget", "shape.bump");
        metrics.TagsOf("shape.tryget.calls").Select(t => t.GetValueOrDefault("shape.value")).Should().Equal(new object?[] { "found", null });
    }

    [Fact]
    public async Task AnAwaitableWithASpanAndAnOutParameter_IsTimedUntilItCompletes()
    {
        using var spans = new Spans();
        using var metrics = new MetricCapture(ShapeService.SourceName);
        IShapeService proxy = new ShapeServiceInstrumented(new ShapeService());

        var parsing = proxy.ParseAsync("abcd".AsSpan(), out var consumed);
        consumed.Should().Be(4);
        spans.Names.Should().BeEmpty();

        (await parsing.ConfigureAwait(true)).Should().Be(40);
        spans.Names.Should().Equal("shape.parse");
        spans.Durations.Should().ContainSingle().Which.Should().BeGreaterThanOrEqualTo(TimeSpan.FromMilliseconds(15));
        metrics.ValuesOf("shape.parse.duration").Should().ContainSingle().Which.Should().BeGreaterThanOrEqualTo(15);
    }

    /// <summary>
    /// The inner method cannot be async, so its exception is synchronous, and the proxy rethrows
    /// it synchronously with or without a listener. With one, the span records the error.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ASynchronousThrow_FromASplitCall_IsRethrownSynchronously(bool listening)
    {
        using var spans = listening ? new Spans() : null;
        IShapeService proxy = new ShapeServiceInstrumented(new ShapeService());

        var call = () => proxy.ParseAsync(ReadOnlySpan<char>.Empty, out _);

        call.Should().Throw<ArgumentException>();
        if (spans is not null)
            spans.Statuses.Should().Equal(ActivityStatusCode.Error);
    }

    [Fact]
    public void PropertiesIndexersEventsAndInheritedMembers_AreForwarded()
    {
        using var spans = new Spans();
        var inner = new ShapeService();
        IShapeService proxy = new ShapeServiceInstrumented(inner);
        var raised = 0;

        proxy.Count = 5;
        proxy.Changed += (_, _) => raised++;
        inner.RaiseChanged();

        inner.Count.Should().Be(5);
        proxy.Count.Should().Be(5);
        proxy[3].Should().Be("item3");
        raised.Should().Be(1);
        proxy.Peek().Should().Be(7);
        spans.Names.Should().Equal("shape.peek");
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
                ShouldListenTo = s => string.Equals(s.Name, ShapeService.SourceName, StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
                ActivityStopped = a =>
                {
                    lock (_gate)
                        _stopped.Add(a);
                },
            };
            ActivitySource.AddActivityListener(_listener);
        }

        public IReadOnlyList<string> Names
        {
            get
            {
                lock (_gate)
                    return [.. _stopped.Select(a => a.OperationName)];
            }
        }

        public IReadOnlyList<TimeSpan> Durations
        {
            get
            {
                lock (_gate)
                    return [.. _stopped.Select(a => a.Duration)];
            }
        }

        public IReadOnlyList<ActivityStatusCode> Statuses
        {
            get
            {
                lock (_gate)
                    return [.. _stopped.Select(a => a.Status)];
            }
        }

        public void Dispose() => _listener.Dispose();
    }
}
