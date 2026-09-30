namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// A nested generic interface, built by the real generator in this project, gets a generic proxy
/// next to it that forwards calls and records its metrics (#162). Before the fix the proxy was a
/// top-level non-generic class that did not compile.
/// </summary>
public sealed partial class NestedGenericProxyTests : IDisposable
{
    private const string MeterName = "ZeroAlloc.Telemetry.Generated.Tests.Store";

    private readonly MetricCapture _capture = new(MeterName);

    [Instrument(MeterName)]
    public interface IStore<TKey, TValue> where TKey : notnull
    {
        [Count("store.reads")]
        TValue? Get(TKey key);
    }

    public void Dispose() => _capture.Dispose();

    [Fact]
    public void Proxy_ForwardsCalls_AndRecordsItsMetrics()
    {
        IStore<string, int[]> proxy = new StoreInstrumented<string, int[]>(new Store());

        proxy.Get("a").Should().Equal(1);
        proxy.Get("b").Should().BeNull();

        _capture.ValuesOf("store.reads").Should().Equal(1d, 1d);
    }

    private sealed class Store : IStore<string, int[]>
    {
        public int[]? Get(string key) => string.Equals(key, "a", StringComparison.Ordinal) ? [1] : null;
    }
}
