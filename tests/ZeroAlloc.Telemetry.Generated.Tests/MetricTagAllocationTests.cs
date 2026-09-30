namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// With no listener on the meter, a tagged call allocates nothing: the tag list is only built for
/// an enabled instrument, so the member is not read and the int shard tag is not boxed.
/// </summary>
/// <remarks>
/// In the same collection as <see cref="MetricTagBehaviorTests"/>, so no listener on this meter is
/// attached while the allocation test runs. Allocations are counted on the current thread only,
/// so tests running on other threads do not disturb the count.
/// </remarks>
[Collection(ModelMeterCollection.Name)]
public sealed class MetricTagAllocationTests
{
    [Fact]
    public void TaggedCall_WithNoListener_AllocatesNothing()
    {
        var proxy = new ModelServiceInstrumented(new FakeModelService
        {
            Result = ModelResult.Success(new ModelReply { Model = "gpt-9-2026-09", Tokens = 12, Shard = 3 }),
        });

        // Warm up: JIT, static field initialisation and instrument creation all allocate once.
        for (var i = 0; i < 100; i++)
            proxy.Complete("hi");

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
            proxy.Complete("hi");
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        allocated.Should().Be(0);
    }

    /// <summary>
    /// The control: the same proxy, once a listener enables its instruments, does build the tags.
    /// Without it, a proxy that never tagged at all would pass the test above.
    /// </summary>
    [Fact]
    public void TaggedCall_WithAListener_BuildsTheTags()
    {
        using var capture = new MetricCapture(ModelMeterCollection.MeterName);
        var proxy = new ModelServiceInstrumented(new FakeModelService
        {
            Result = ModelResult.Success(new ModelReply { Model = "gpt-9-2026-09", Tokens = 12, Shard = 3 }),
        });

        proxy.Complete("hi");

        capture.TagsOf("model.tokens").Should().ContainSingle().Which.Should().ContainKey("model.shard");
    }
}
