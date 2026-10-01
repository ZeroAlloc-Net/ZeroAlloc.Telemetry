namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>Its source and meter take the version of this assembly (#172).</summary>
[Instrument(VersionedService.DefaultSource)]
public interface IVersionedService
{
    [Trace("versioned.run")]
    [Count("versioned.calls")]
    Task<int> RunAsync();
}
