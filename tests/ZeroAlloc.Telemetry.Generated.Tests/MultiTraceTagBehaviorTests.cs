using System.Diagnostics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Several <c>[TraceTag]</c> on one parameter (#181), observed with an <c>ActivityListener</c>:
/// every tag is on the span, and with <c>TagsAtStart</c> the sampler sees them all.
/// </summary>
/// <remarks>The class is the only user of its source, so its tests run sequentially and in isolation.</remarks>
public sealed class MultiTraceTagBehaviorTests
{
    private static readonly Uri Endpoint = new("https://api.example.test:8443/v1");

    private static readonly AskRequest Request = new() { Model = "gpt-9", Questions = ["a", "b", "c"] };

    private static readonly Dictionary<string, object?> Expected = new(StringComparer.Ordinal)
    {
        ["server.address"] = "api.example.test",
        ["server.port"] = 8443,
        ["gen_ai.request.model"] = "gpt-9",
        ["jev.question.count"] = 3,
    };

    [Fact]
    public async Task EveryTagOfAParameter_IsSetOnTheSpan()
    {
        using var spans = new Capture();
        var proxy = new EndpointServiceInstrumented(new EndpointService());

        (await proxy.AskAsync(Endpoint, Request).ConfigureAwait(true)).Should().Be(3);

        spans.Stopped.Should().ContainSingle().Which.TagObjects
            .ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal)
            .Should().Equal(Expected);
        spans.SamplerTags.Should().ContainSingle().Which.Should().BeEmpty();
    }

    [Fact]
    public async Task EveryTagOfAParameter_ReachesTheSampler_WithTagsAtStart()
    {
        using var spans = new Capture();
        var proxy = new EndpointServiceInstrumented(new EndpointService());

        await proxy.SampledAsync(Endpoint, Request).ConfigureAwait(true);

        spans.SamplerTags.Should().ContainSingle().Which.Should().Equal(Expected);
        spans.Stopped.Should().ContainSingle().Which.TagObjects
            .ToDictionary(t => t.Key, t => t.Value, StringComparer.Ordinal)
            .Should().Equal(Expected);
    }

    private sealed class Capture : IDisposable
    {
        private readonly ActivityListener _listener;
        private readonly Lock _gate = new();
        private readonly List<Activity> _stopped = [];
        private readonly List<Dictionary<string, object?>> _samplerTags = [];

        public Capture()
        {
            _listener = new ActivityListener
            {
                ShouldListenTo = s => string.Equals(s.Name, EndpointService.SourceName, StringComparison.Ordinal),
                Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
                {
                    var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
                    if (options.Tags is { } tags)
                    {
                        foreach (var tag in tags)
                            copy.Add(tag.Key, tag.Value);
                    }

                    lock (_gate)
                        _samplerTags.Add(copy);
                    return ActivitySamplingResult.AllDataAndRecorded;
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
