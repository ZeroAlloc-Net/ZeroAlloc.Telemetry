using System.Diagnostics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// The GenAI client span through the real generator (#170): kind, error status from the result,
/// the name from parameters, and tags a sampler sees.
/// </summary>
/// <remarks>The class is the only user of its source, so its tests run sequentially and in isolation.</remarks>
public sealed class GenAiSpanTests
{
    [Fact]
    public async Task ASampledCall_IsAClientSpan_NamedFromItsParameters_WithTagsTheSamplerSees()
    {
        using var spans = new SpanCapture(ActivitySamplingResult.AllDataAndRecorded);
        var proxy = new GenAiChatInstrumented(new GenAiChat());

        await proxy.ChatAsync("chat", new GenAiRequest { Model = "gpt-9" }, CancellationToken.None).ConfigureAwait(true);

        var span = spans.Stopped.Should().ContainSingle().Subject;
        span.Kind.Should().Be(ActivityKind.Client);
        span.OperationName.Should().Be("ChatAsync");
        span.DisplayName.Should().Be("chat gpt-9");
        span.Status.Should().Be(ActivityStatusCode.Unset);
        span.GetTagItem("gen_ai.response.model").Should().Be("gpt-9-2026-09");

        var seen = spans.SamplerTags.Should().ContainSingle().Subject;
        seen.Should().Equal(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["gen_ai.provider.name"] = "openai",
            ["gen_ai.operation.name"] = "chat",
            ["gen_ai.request.model"] = "gpt-9",
        });
        span.GetTagItem("gen_ai.provider.name").Should().Be("openai");
    }

    [Fact]
    public async Task AFailedResult_MarksTheSpanAsAnError_WithItsDescription()
    {
        using var spans = new SpanCapture(ActivitySamplingResult.AllDataAndRecorded);
        var proxy = new GenAiChatInstrumented(new GenAiChat());

        var result = await proxy.ChatAsync("chat", new GenAiRequest { Model = "gpt-9", Fail = true }, CancellationToken.None).ConfigureAwait(true);

        result.IsFailure.Should().BeTrue();
        var span = spans.Stopped.Should().ContainSingle().Subject;
        span.Status.Should().Be(ActivityStatusCode.Error);
        span.StatusDescription.Should().Be("rate limited");
    }

    /// <summary>
    /// A listener that samples nothing gets no span, so the display name is never composed: a call
    /// whose name has a parameter token allocates exactly what one with a constant name does,
    /// which is whatever <c>StartActivity</c> itself costs with a listener attached.
    /// </summary>
    [Fact]
    public void AnUnsampledCall_ComposesNoName()
    {
        using var spans = new SpanCapture(ActivitySamplingResult.None);
        var proxy = new GenAiChatInstrumented(new GenAiChat());

        var templated = Measure(() => proxy.Embed("text-embedding-9"));
        var constant = Measure(() => proxy.EmbedPlain("text-embedding-9"));

        templated.Should().Be(constant);
        spans.Stopped.Should().BeEmpty();
    }

    private static long Measure(Func<int> call)
    {
        for (var i = 0; i < 100; i++)
            call();

        var before = GC.GetAllocatedBytesForCurrentThread();
        for (var i = 0; i < 1_000; i++)
            call();
        return GC.GetAllocatedBytesForCurrentThread() - before;
    }

    [Fact]
    public void ASampledCall_IsNamedFromItsParameter()
    {
        using var spans = new SpanCapture(ActivitySamplingResult.AllDataAndRecorded);
        var proxy = new GenAiChatInstrumented(new GenAiChat());

        proxy.Embed("text-embedding-9").Should().Be(16);

        var span = spans.Stopped.Should().ContainSingle().Subject;
        span.OperationName.Should().Be("embeddings");
        span.DisplayName.Should().Be("embeddings text-embedding-9");
        span.Kind.Should().Be(ActivityKind.Internal);
    }

    private sealed class SpanCapture : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly Lock _gate = new();
        private readonly List<Activity> _stopped = [];
        private readonly List<Dictionary<string, object?>> _samplerTags = [];

        public SpanCapture(ActivitySamplingResult sampling)
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = s => string.Equals(s.Name, GenAiChat.SourceName, StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                {
                    if (sampling != ActivitySamplingResult.None && options.Tags is { } tags)
                    {
                        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
                        foreach (var tag in tags)
                            copy.Add(tag.Key, tag.Value);
                        lock (_gate)
                            _samplerTags.Add(copy);
                    }

                    return sampling;
                },
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

        public IReadOnlyList<Dictionary<string, object?>> SamplerTags
        {
            get
            {
                lock (_gate)
                    return [.. _samplerTags];
            }
        }

        public void Dispose() => _listener.Dispose();
    }
}
