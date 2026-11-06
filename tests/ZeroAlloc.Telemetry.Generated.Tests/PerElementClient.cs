namespace ZeroAlloc.Telemetry.Generated.Tests;

public sealed class PerElementClient(PerElementResponse response) : IPerElementClient
{
    public const string MeterName = "ZeroAlloc.Telemetry.Generated.Tests.PerElement";

    public PerElementResponse Evaluate() => response;
}
