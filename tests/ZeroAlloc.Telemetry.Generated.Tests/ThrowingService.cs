namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>Fails every call with <see cref="Exception"/>, in the way <see cref="Failure"/> says.</summary>
public sealed class ThrowingService : IThrowingService
{
    public const string SourceName = "ZeroAlloc.Telemetry.Generated.Tests.Throwing";

    public required Failure Failure { get; init; }

    public required Exception Exception { get; init; }

    public Task RunAsync() => Fail<int>();

    public ValueTask StepAsync() => new(Fail<int>());

    public ValueTask<int> ReadAsync() => new(Fail<int>());

    public Task<int> PlainAsync() => Fail<int>();

    private Task<T> Fail<T>()
    {
        if (Failure != Failure.AsynchronousThrow)
            throw Exception;

        return Later<T>();
    }

    private async Task<T> Later<T>()
    {
        await Task.Yield();
        throw Exception;
    }
}
