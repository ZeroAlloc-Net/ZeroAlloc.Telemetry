namespace ZeroAlloc.Telemetry.AotSmoke;

public readonly struct OrderDecision
{
    public bool IsRejected { get; init; }

    public string? Reason { get; init; }
}
