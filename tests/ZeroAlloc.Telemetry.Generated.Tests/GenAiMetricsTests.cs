using System.Diagnostics.Metrics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// The GenAI client metrics through the real generator (#171): durations in seconds, bucket
/// advice, constant and parameter tags on both paths, and a histogram per token count.
/// </summary>
/// <remarks>The class is the only user of its meter, so its tests run sequentially and in isolation.</remarks>
public sealed class GenAiMetricsTests
{
    private const string Duration = "gen_ai.client.operation.duration";
    private const string TokenUsage = "gen_ai.client.token.usage";

    [Fact]
    public async Task Duration_IsRecordedInSeconds_WithItsBucketAdvice()
    {
        using var capture = new MetricCapture(GenAiClient.MeterName);
        var proxy = new GenAiClientInstrumented(new GenAiClient());

        await proxy.ChatAsync(new GenAiRequest { Model = "gpt-9" }, CancellationToken.None).ConfigureAwait(true);

        // The inner call takes 30 ms: 0.03 s. In milliseconds it would be at least 30.
        capture.ValuesOf(Duration).Should().ContainSingle().Which.Should().BeInRange(0.02, 5);
        var histogram = (Histogram<double>)capture.Published(Duration);
        histogram.Unit.Should().Be("s");
        histogram.Advice!.HistogramBucketBoundaries.Should().Equal(0.01, 0.02, 0.04, 0.08, 0.16, 0.32, 0.64, 1.28, 2.56, 5.12);
        ((Histogram<double>)capture.Published(TokenUsage)).Advice!.HistogramBucketBoundaries.Should().Equal(1, 4, 16, 64);
    }

    [Fact]
    public async Task EveryTokenCount_IsRecorded_WithTheConstantAndParameterTags()
    {
        using var capture = new MetricCapture(GenAiClient.MeterName);
        var proxy = new GenAiClientInstrumented(new GenAiClient());

        await proxy.ChatAsync(new GenAiRequest { Model = "gpt-9" }, CancellationToken.None).ConfigureAwait(true);

        capture.ValuesOf(TokenUsage).Should().Equal(12d, 34d);
        capture.TagsOf(TokenUsage).Should().AllSatisfy(tags => tags.Should().Equal(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["gen_ai.operation.name"] = "chat",
            ["gen_ai.provider.name"] = "openai",
            ["gen_ai.token.modality"] = "text",
            ["gen_ai.request.model"] = "gpt-9",
        }));
        capture.TagsOf(Duration).Should().ContainSingle().Which.Should().Equal(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["gen_ai.operation.name"] = "chat",
            ["gen_ai.provider.name"] = "openai",
            ["gen_ai.request.model"] = "gpt-9",
        });
    }

    [Fact]
    public async Task AFailedCall_RecordsItsDuration_WithTheTagsKnownBeforeTheCall()
    {
        using var capture = new MetricCapture(GenAiClient.MeterName);
        var proxy = new GenAiClientInstrumented(new GenAiClient());

        var call = async () => await proxy.ChatAsync(new GenAiRequest { Model = "gpt-9", Fail = true }, CancellationToken.None).ConfigureAwait(true);
        await call.Should().ThrowAsync<InvalidOperationException>().ConfigureAwait(true);

        capture.ValuesOf(TokenUsage).Should().BeEmpty();
        capture.ValuesOf(Duration).Should().ContainSingle().Which.Should().BeInRange(0.02, 5);
        capture.TagsOf(Duration).Should().ContainSingle().Which.Should().ContainKeys("gen_ai.operation.name", "gen_ai.provider.name", "gen_ai.request.model");
    }

    [Fact]
    public void PerElementHistogram_WithNoListener_AllocatesNothing()
    {
        var proxy = new GenAiClientInstrumented(new GenAiClient());
        Measure(proxy, 100);

        Measure(proxy, 1_000).Should().Be(0);
    }

    /// <summary>
    /// With a listener the elements are iterated and the tags built, still without allocating:
    /// the tag list is a struct, the values are strings, and the span is not copied.
    /// </summary>
    [Fact]
    public void PerElementHistogram_WithAListener_RecordsEveryElement_WithoutAllocating()
    {
        long recorded = 0;
        using var listener = new MeterListener();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (string.Equals(instrument.Meter.Name, GenAiClient.MeterName, StringComparison.Ordinal))
                l.EnableMeasurementEvents(instrument);
        };
        listener.SetMeasurementEventCallback<double>((_, _, _, _) => recorded++);
        listener.Start();

        var proxy = new GenAiClientInstrumented(new GenAiClient());
        Measure(proxy, 100);
        recorded = 0;

        Measure(proxy, 1_000).Should().Be(0);
        recorded.Should().Be(2_000);
    }

    private static long Measure(GenAiClientInstrumented proxy, int calls)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < calls; i++)
            proxy.Measure("text-embedding-9");
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }
}
