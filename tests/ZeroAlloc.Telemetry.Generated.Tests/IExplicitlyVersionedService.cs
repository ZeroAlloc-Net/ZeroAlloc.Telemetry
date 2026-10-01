namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>Its source and meter take the version set on <c>[Instrument]</c> (#172).</summary>
[Instrument(VersionedService.ExplicitSource, Version = "7.1.0-test")]
public interface IExplicitlyVersionedService
{
    [Count("explicit.calls")]
    void Run();
}
