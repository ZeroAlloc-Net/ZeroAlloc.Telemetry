namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>Token counts per modality, as a struct with a memory, so reading them allocates nothing.</summary>
public readonly struct GenAiUsage
{
    public ReadOnlyMemory<double> Tokens { get; init; }
}
