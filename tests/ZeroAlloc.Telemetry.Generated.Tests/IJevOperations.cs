namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>The ZeroAlloc.Jev shape: generic methods returning a result struct (#168).</summary>
[Instrument("ZeroAlloc.Jev")]
internal interface IJevOperations
{
    [Trace("jev.union")]
    [Count("jev.operations")]
    [Histogram("jev.duration", Unit = "ms")]
    ValueTask<Result<T, string>> UnionAsync<T>(T left, T right) where T : ISet<T>, new();

    [Trace("jev.evaluate")]
    ValueTask<Result<T, E>> EvaluateAsync<T, E>(T input) where T : ISet<T> where E : notnull;
}
