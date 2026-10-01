using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Reflection;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Through the real generator (#172): the proxy does not resume on its caller's
/// synchronization context, and its source and meter carry a version.
/// </summary>
public sealed class HygieneBehaviorTests
{
    [Fact]
    public async Task TheProxy_DoesNotResumeOnTheCallersContext()
    {
        using var metrics = new MetricCapture(VersionedService.DefaultSource);
        var proxy = new VersionedServiceInstrumented(new VersionedService());
        var context = new CountingContext();

        var previous = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(context);
        Task<int> call;
        try
        {
            call = proxy.RunAsync();
        }
        finally
        {
            SynchronizationContext.SetSynchronizationContext(previous);
        }

        (await call.ConfigureAwait(true)).Should().Be(1);
        metrics.ValuesOf("versioned.calls").Should().Equal(1d);
        context.Posts.Should().Be(0);
    }

    [Fact]
    public void TheSourceAndMeter_DefaultToTheAssemblysInformationalVersion()
    {
        var expected = typeof(HygieneBehaviorTests).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()!.InformationalVersion;

        var (source, meter) = Versions(VersionedService.DefaultSource, () =>
            new VersionedServiceInstrumented(new VersionedService()).RunAsync().GetAwaiter().GetResult());

        expected.Should().NotBeNullOrEmpty();
        source.Should().Be(expected);
        meter.Should().Be(expected);
    }

    [Fact]
    public void TheSourceAndMeter_TakeTheVersionSetOnInstrument()
    {
        var (_, meter) = Versions(VersionedService.ExplicitSource, () =>
            new ExplicitlyVersionedServiceInstrumented(new VersionedService()).Run());

        meter.Should().Be("7.1.0-test");
    }

    private static (string? Source, string? Meter) Versions(string name, Action call)
    {
        string? source = null;
        string? meter = null;
        using var activities = new ActivityListener
        {
            ShouldListenTo = s =>
            {
                if (string.Equals(s.Name, name, StringComparison.Ordinal))
                    source = s.Version;
                return false;
            },
        };
        ActivitySource.AddActivityListener(activities);

        using var meters = new MeterListener();
        meters.InstrumentPublished = (instrument, _) =>
        {
            if (string.Equals(instrument.Meter.Name, name, StringComparison.Ordinal))
                meter = instrument.Meter.Version;
        };
        meters.Start();

        call();
        return (source, meter);
    }

    /// <summary>Counts the continuations posted to it, and runs them on the thread pool.</summary>
    private sealed class CountingContext : SynchronizationContext
    {
        private int _posts;

        public int Posts => Volatile.Read(ref _posts);

        public override void Post(SendOrPostCallback d, object? state)
        {
            Interlocked.Increment(ref _posts);
            ThreadPool.QueueUserWorkItem(_ => d(state));
        }
    }
}
