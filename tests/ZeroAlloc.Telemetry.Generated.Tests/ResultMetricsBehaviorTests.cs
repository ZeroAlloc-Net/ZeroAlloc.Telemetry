namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Runs the generator's own proxy, not a hand-written copy, and observes it with a
/// <see cref="System.Diagnostics.Metrics.MeterListener"/>. xUnit creates a new instance per test,
/// so each test gets its own capture. The class is the only user of its meter, so its tests run
/// sequentially and in isolation.
/// </summary>
public sealed class ResultMetricsBehaviorTests : IDisposable
{
    private const string MeterName = "ZeroAlloc.Telemetry.Generated.Tests.Chat";

    private readonly MetricCapture _capture = new(MeterName);

    public void Dispose() => _capture.Dispose();

    [Fact]
    public async Task Success_RecordsTheValuesCarriedByTheResult()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService
        {
            Result = ChatResult.Success(new TokenUsage { Input = 120, Output = 45, Cost = 0.25m }),
        });

        await proxy.CompleteAsync("hi", CancellationToken.None).ConfigureAwait(true);

        _capture.ValuesOf("chat.tokens.input").Should().Equal(120d);
        _capture.ValuesOf("chat.tokens.output").Should().Equal(45d);
        _capture.ValuesOf("chat.cost").Should().Equal(0.25d);
        _capture.ValuesOf("chat.completions").Should().Equal(1d);
        _capture.ValuesOf("chat.duration").Should().ContainSingle().Which.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task FailedResult_RecordsNothing_AndIsNotCounted()
    {
        // ChatResult.Value throws on failure, so reaching the assertions also proves no guarded
        // member was read.
        var proxy = new ChatServiceInstrumented(new FakeChatService { Result = ChatResult.Failure() });

        await proxy.CompleteAsync("hi", CancellationToken.None).ConfigureAwait(true);

        _capture.ValuesOf("chat.completions").Should().BeEmpty();
        _capture.ValuesOf("chat.duration").Should().BeEmpty();
        _capture.ValuesOf("chat.tokens.input").Should().BeEmpty();
        _capture.ValuesOf("chat.tokens.output").Should().BeEmpty();
        _capture.ValuesOf("chat.cost").Should().BeEmpty();
    }

    [Fact]
    public async Task NullMember_IsNotRecorded()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService
        {
            Result = ChatResult.Success(new TokenUsage { Input = 7, Output = null }),
        });

        await proxy.CompleteAsync("hi", CancellationToken.None).ConfigureAwait(true);

        _capture.ValuesOf("chat.tokens.input").Should().Equal(7d);
        _capture.ValuesOf("chat.tokens.output").Should().BeEmpty();
    }

    [Fact]
    public async Task NullReturnValue_IsNotRecorded()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService { Score = null });

        await proxy.ScoreAsync(CancellationToken.None).ConfigureAwait(true);

        _capture.ValuesOf("chat.score").Should().BeEmpty();
    }

    [Fact]
    public async Task NullableReturnValue_IsRecorded_WhenPresent()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService { Score = 0.9 });

        await proxy.ScoreAsync(CancellationToken.None).ConfigureAwait(true);

        _capture.ValuesOf("chat.score").Should().Equal(0.9d);
    }

    [Fact]
    public async Task Throw_RecordsNothing_ForGuardedInstruments()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService { Throw = true });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => proxy.CompleteAsync("hi", CancellationToken.None)).ConfigureAwait(true);

        _capture.ValuesOf("chat.duration").Should().BeEmpty();
        _capture.ValuesOf("chat.completions").Should().BeEmpty();
    }

    [Fact]
    public async Task UnitAndDescription_ReachThePublishedInstrument()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService
        {
            Result = ChatResult.Success(new TokenUsage { Input = 1 }),
        });

        await proxy.CompleteAsync("hi", CancellationToken.None).ConfigureAwait(true);

        _capture.Published("chat.tokens.input").Unit.Should().Be("{token}");
        _capture.Published("chat.tokens.input").Description.Should().Be("Prompt tokens consumed");
        _capture.Published("chat.completions").Unit.Should().Be("{completion}");
        _capture.Published("chat.completions").Description.Should().Be("Successful completions");
        _capture.Published("chat.cost").Unit.Should().Be("USD");
        _capture.Published("chat.duration").Unit.Should().Be("ms");
    }

    [Fact]
    public async Task UnguardedHistogram_StillRecords_OnThrow()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService { Throw = true });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => proxy.PingAsync(CancellationToken.None)).ConfigureAwait(true);

        _capture.ValuesOf("chat.raw_duration").Should().ContainSingle().Which.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task ResultMetric_Records_WithoutATrace()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService());

        var retries = await proxy.RetryCountAsync(CancellationToken.None).ConfigureAwait(true);

        retries.Should().Be(3);
        _capture.ValuesOf("chat.retries").Should().Equal(3d);
    }
}
