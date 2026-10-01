namespace ZeroAlloc.Telemetry.Generated.Tests;

public sealed class AskRequest
{
    public required string Model { get; init; }

    public IReadOnlyList<string> Questions { get; init; } = [];
}
