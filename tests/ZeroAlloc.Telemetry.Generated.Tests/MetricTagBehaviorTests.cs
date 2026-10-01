namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Runs the generated proxy for <c>[MetricTagFromResult]</c> and reads the tags a
/// <see cref="System.Diagnostics.Metrics.MeterListener"/> receives.
/// </summary>
[Collection(ModelMeterCollection.Name)]
public sealed class MetricTagBehaviorTests : IDisposable
{
    private readonly MetricCapture _capture = new(ModelMeterCollection.MeterName);

    public void Dispose() => _capture.Dispose();

    [Fact]
    public void Success_TagsEveryMetric_AndTheFilteredTagOnlyItsMetric()
    {
        var proxy = new ModelServiceInstrumented(new FakeModelService
        {
            Result = ModelResult.Success(new ModelReply { Model = "gpt-9-2026-09", Tokens = 12, Shard = 3 }),
        });

        proxy.Complete("hi");

        _capture.ValuesOf("model.tokens").Should().Equal(12d);
        _capture.TagsOf("model.tokens").Should().ContainSingle().Which.Should().BeEquivalentTo(
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["gen_ai.response.model"] = "gpt-9-2026-09",
                ["model.shard"] = 3,
            });

        var modelOnly = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["gen_ai.response.model"] = "gpt-9-2026-09",
        };
        _capture.TagsOf("model.calls").Should().ContainSingle().Which.Should().BeEquivalentTo(modelOnly);
        _capture.TagsOf("model.duration").Should().ContainSingle().Which.Should().BeEquivalentTo(modelOnly);
    }

    [Fact]
    public void FailedResult_RecordsUnguardedMetrics_WithoutGuardedTags()
    {
        // ModelResult.Value throws on failure, so reaching the assertions also proves the guarded
        // tags were not read.
        var proxy = new ModelServiceInstrumented(new FakeModelService { Result = ModelResult.Failure() });

        proxy.Complete("hi");

        _capture.TagsOf("model.calls").Should().ContainSingle().Which.Should().BeEmpty();
        _capture.TagsOf("model.duration").Should().ContainSingle().Which.Should().BeEmpty();
        _capture.ValuesOf("model.tokens").Should().BeEmpty();
    }

    [Fact]
    public void NullTagValue_AddsNoTag_AndStillRecords()
    {
        var proxy = new ModelServiceInstrumented(new FakeModelService
        {
            Result = ModelResult.Success(new ModelReply { Model = null, Tokens = 5, Shard = 1 }),
        });

        proxy.Complete("hi");

        _capture.TagsOf("model.calls").Should().ContainSingle().Which.Should().BeEmpty();
        _capture.TagsOf("model.tokens").Should().ContainSingle().Which.Should().BeEquivalentTo(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["model.shard"] = 1 });
    }

    [Fact]
    public async Task Throw_RecordsTheUnguardedHistogram_WithOnlyTheErrorType()
    {
        var proxy = new ModelServiceInstrumented(new FakeModelService { Throw = true });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => proxy.PingAsync(CancellationToken.None)).ConfigureAwait(true);

        // The result tag cannot apply without a result; the exception classifies the point (#184).
        _capture.TagsOf("model.ping").Should().ContainSingle().Which.Should().BeEquivalentTo(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["error.type"] = "System.InvalidOperationException" });
    }

    [Fact]
    public async Task AsyncResult_IsTagged()
    {
        var proxy = new ModelServiceInstrumented(new FakeModelService
        {
            Result = ModelResult.Success(new ModelReply { Model = "m-1" }),
        });

        await proxy.PingAsync(CancellationToken.None).ConfigureAwait(true);

        _capture.TagsOf("model.ping").Should().ContainSingle().Which.Should().BeEquivalentTo(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["gen_ai.response.model"] = "m-1" });
    }
}
