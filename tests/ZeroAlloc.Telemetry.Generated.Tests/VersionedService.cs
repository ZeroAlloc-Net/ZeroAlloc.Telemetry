namespace ZeroAlloc.Telemetry.Generated.Tests;

public sealed class VersionedService : IVersionedService, IExplicitlyVersionedService
{
    public const string DefaultSource = "ZeroAlloc.Telemetry.Generated.Tests.Versioned";
    public const string ExplicitSource = "ZeroAlloc.Telemetry.Generated.Tests.ExplicitlyVersioned";

    public async Task<int> RunAsync()
    {
        await Task.Delay(10).ConfigureAwait(false);
        return 1;
    }

    public void Run()
    {
    }
}
