using System.Diagnostics;
using ZeroAlloc.Telemetry;

// xUnit's Assert.Single is not LINQ's Single; ConfigureAwait is irrelevant in xUnit tests and
// prohibited by xUnit1030 in async test methods.
#pragma warning disable HLQ005, MA0004

namespace ZeroAlloc.Telemetry.Tests;

/// <summary>
/// Runtime behaviour of span tags (Telemetry#30). Mirrors the shape the generator emits, so the
/// assertions are about what an ActivityListener actually observes rather than about generator
/// output — the snapshot tests cover the emission itself.
/// </summary>
public class SpanTagBehaviorTests
{
    [Instrument("TagTestSource")]
    private interface ISearchService
    {
        [Trace("search.run")]
        [TraceTagFromResult("search.result.count", "Count")]
        ValueTask<IReadOnlyList<string>> SearchAsync(
            [TraceTag("search.collection")] string collection,
            [TraceTag("search.top_k")] int topK,
            CancellationToken ct);
    }

#pragma warning disable EPC12, MA0004
    private sealed class SearchServiceInstrumented : ISearchService
    {
        private static readonly ActivitySource _activitySource = new("TagTestSource");

        private readonly ISearchService _inner;
        public SearchServiceInstrumented(ISearchService inner) => _inner = inner;

        public async ValueTask<IReadOnlyList<string>> SearchAsync(string collection, int topK, CancellationToken ct)
        {
            using var _activity = _activitySource.StartActivity("search.run");
            _activity?.SetTag("search.collection", collection);
            _activity?.SetTag("search.top_k", topK);
            try
            {
                var _result = await _inner.SearchAsync(collection, topK, ct);
                // Copy, not _result directly: null-testing _result would leave it maybe-null and
                // make `return _result;` raise CS8603. This mirrors the generator exactly.
                var _tagged = _result;
                _activity?.SetTag("search.result.count", _tagged?.Count);
                return _result;
            }
            catch (Exception _ex)
            {
                _activity?.SetStatus(ActivityStatusCode.Error, _ex.Message);
                throw;
            }
        }
    }
    // MA0004 stays disabled for the rest of the file: the pragma above the namespace covers the
    // test methods too, and restoring it here would re-enable it for them.
#pragma warning restore EPC12

    private sealed class FakeSearch : ISearchService
    {
        public ValueTask<IReadOnlyList<string>> SearchAsync(string collection, int topK, CancellationToken ct)
            => new(new[] { "a", "b", "c" });
    }

    private sealed class ThrowingSearch : ISearchService
    {
        public ValueTask<IReadOnlyList<string>> SearchAsync(string collection, int topK, CancellationToken ct)
            => throw new InvalidOperationException("boom");
    }

    private static ActivityListener Listen(List<Activity> captured, ActivitySamplingResult sampling = ActivitySamplingResult.AllDataAndRecorded)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = s => string.Equals(s.Name, "TagTestSource", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => sampling,
            ActivityStopped = captured.Add,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task ArgumentTags_AreRecordedOnTheSpan()
    {
        var captured = new List<Activity>();
        using var listener = Listen(captured);

        var sut = new SearchServiceInstrumented(new FakeSearch());
        await sut.SearchAsync("documents", 5, CancellationToken.None);

        var activity = Assert.Single(captured);
        Assert.Equal("documents", activity.GetTagItem("search.collection"));
        Assert.Equal(5, activity.GetTagItem("search.top_k"));
    }

    [Fact]
    public async Task ResultTag_RecordsMemberOfAwaitedValue()
    {
        var captured = new List<Activity>();
        using var listener = Listen(captured);

        var sut = new SearchServiceInstrumented(new FakeSearch());
        await sut.SearchAsync("documents", 5, CancellationToken.None);

        // Three results from FakeSearch — read from the awaited value, not the task.
        var activity = Assert.Single(captured);
        Assert.Equal(3, activity.GetTagItem("search.result.count"));
    }

    [Fact]
    public async Task ResultTag_IsAbsent_WhenTheCallThrows()
    {
        var captured = new List<Activity>();
        using var listener = Listen(captured);

        var sut = new SearchServiceInstrumented(new ThrowingSearch());
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            await sut.SearchAsync("documents", 5, CancellationToken.None));

        var activity = Assert.Single(captured);
        // Argument tags survive because they are set before the call; the result tag cannot be
        // set because there is no result, and the span is marked failed instead.
        Assert.Equal("documents", activity.GetTagItem("search.collection"));
        Assert.Null(activity.GetTagItem("search.result.count"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
    }

    [Fact]
    public async Task NoListener_ProducesNoActivity_AndTaggingIsSkipped()
    {
        // No listener registered at all: StartActivity returns null, so every _activity?.SetTag
        // short-circuits — the argument is never evaluated and value types are never boxed.
        var sut = new SearchServiceInstrumented(new FakeSearch());

        var result = await sut.SearchAsync("documents", 5, CancellationToken.None);

        Assert.Equal(3, result.Count);
        Assert.Null(Activity.Current);
    }

    [Fact]
    public async Task TagsSetInsideTheImplementation_AttachToTheProxySpan()
    {
        // The documented escape hatch for state the proxy cannot see: Activity.Current inside the
        // wrapped implementation is the span the proxy started.
        var captured = new List<Activity>();
        using var listener = Listen(captured);

        var sut = new SearchServiceInstrumented(new InnerTagging());
        await sut.SearchAsync("documents", 5, CancellationToken.None);

        var activity = Assert.Single(captured);
        Assert.Equal("computed-inside", activity.GetTagItem("search.internal"));
    }

    private sealed class InnerTagging : ISearchService
    {
        public ValueTask<IReadOnlyList<string>> SearchAsync(string collection, int topK, CancellationToken ct)
        {
            Activity.Current?.SetTag("search.internal", "computed-inside");
            return new(new[] { "a" });
        }
    }
}
