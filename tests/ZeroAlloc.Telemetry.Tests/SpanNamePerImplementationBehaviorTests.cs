using System.Diagnostics;
using ZeroAlloc.Telemetry;

// xUnit's Assert.Single is not LINQ's Single; ConfigureAwait is irrelevant in xUnit tests and
// prohibited by xUnit1030 in async test methods.
#pragma warning disable HLQ005, MA0004

namespace ZeroAlloc.Telemetry.Tests;

/// <summary>
/// Runtime behaviour of the <c>{type}</c> span-name token (Telemetry#53). Mirrors the shape the
/// generator emits, so the assertions are about what an ActivityListener actually observes.
/// <para>
/// The point of the feature is that two implementations of one interface are distinguishable in a
/// trace. Asserting on generated text cannot show that; only two live proxies can.
/// </para>
/// </summary>
public class SpanNamePerImplementationBehaviorTests
{
    [Instrument("SpanNameTestSource")]
    private interface IVectorStore
    {
        [Trace("vectorstore.search.{type}")]
        ValueTask<int> SearchAsync(string query, CancellationToken ct);
    }

    /// <summary>
    /// Mirrors the generated proxy exactly: the name is composed once in the constructor from
    /// the wrapped instance's type, and the call path only reads the field.
    /// </summary>
#pragma warning disable EPC12, MA0004
    private sealed class VectorStoreInstrumented : IVectorStore
    {
        private static readonly ActivitySource _activitySource = new("SpanNameTestSource");

        private readonly IVectorStore _inner;
        private readonly string _spanName_SearchAsync_0;

        public VectorStoreInstrumented(IVectorStore inner)
        {
            _inner = inner;
            var _implName = inner.GetType().Name;
            _spanName_SearchAsync_0 = "vectorstore.search." + _implName;
        }

        public async ValueTask<int> SearchAsync(string query, CancellationToken ct)
        {
            using var _activity = _activitySource.StartActivity(_spanName_SearchAsync_0);
            try
            {
                var _result = await _inner.SearchAsync(query, ct);
                return _result;
            }
            catch (Exception _ex)
            {
                _activity?.SetStatus(ActivityStatusCode.Error, _ex.Message);
                throw;
            }
        }
    }
#pragma warning restore EPC12, MA0004

    private sealed class QdrantVectorStore : IVectorStore
    {
        public ValueTask<int> SearchAsync(string query, CancellationToken ct) => new(1);
    }

    private sealed class WeaviateVectorStore : IVectorStore
    {
        public ValueTask<int> SearchAsync(string query, CancellationToken ct) => new(2);
    }

    private static ActivityListener Listen(List<Activity> captured)
    {
        var listener = new ActivityListener
        {
            ShouldListenTo = s => string.Equals(s.Name, "SpanNameTestSource", StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = captured.Add,
        };
        ActivitySource.AddActivityListener(listener);
        return listener;
    }

    [Fact]
    public async Task TwoImplementations_ProduceDistinctSpanNames()
    {
        var captured = new List<Activity>();
        using var listener = Listen(captured);

        await new VectorStoreInstrumented(new QdrantVectorStore()).SearchAsync("q", CancellationToken.None);
        await new VectorStoreInstrumented(new WeaviateVectorStore()).SearchAsync("q", CancellationToken.None);

        Assert.Equal(2, captured.Count);
        Assert.Equal("vectorstore.search.QdrantVectorStore", captured[0].OperationName);
        Assert.Equal("vectorstore.search.WeaviateVectorStore", captured[1].OperationName);
    }

    /// <summary>
    /// The whole point: before this, both spans carried the interface-level name and a trace
    /// could not attribute cost to a backend.
    /// </summary>
    [Fact]
    public async Task SpanNames_AreNotCollapsedToTheInterfaceName()
    {
        var captured = new List<Activity>();
        using var listener = Listen(captured);

        await new VectorStoreInstrumented(new QdrantVectorStore()).SearchAsync("q", CancellationToken.None);
        await new VectorStoreInstrumented(new WeaviateVectorStore()).SearchAsync("q", CancellationToken.None);

        Assert.Equal(2, captured.Select(a => a.OperationName).Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(captured, a => string.Equals(a.OperationName, "vectorstore.search.{type}", StringComparison.Ordinal));
    }

    /// <summary>
    /// The name is resolved per proxy, so repeated calls through one proxy stay stable — and
    /// resolving in the constructor means no string is composed on the call path.
    /// </summary>
    [Fact]
    public async Task SpanName_IsStableAcrossCallsThroughOneProxy()
    {
        var captured = new List<Activity>();
        using var listener = Listen(captured);

        var sut = new VectorStoreInstrumented(new QdrantVectorStore());
        await sut.SearchAsync("one", CancellationToken.None);
        await sut.SearchAsync("two", CancellationToken.None);

        Assert.Equal(2, captured.Count);
        Assert.All(captured, a => Assert.Equal("vectorstore.search.QdrantVectorStore", a.OperationName));
    }
}
