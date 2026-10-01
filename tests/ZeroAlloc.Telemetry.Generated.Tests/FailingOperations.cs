namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>Fails every call with an exception whose message stands in for request content.</summary>
public sealed class FailingOperations : IFailingOperations
{
    public const string SourceName = "ZeroAlloc.Telemetry.Generated.Tests.Failing";

    public const string Message = "prompt: the user's secret question";

    public Task DescribedAsync() => Later();

    public Task QuietAsync() => Later();

    public int Untraced() => throw new InvalidOperationException(Message);

    private static async Task Later()
    {
        await Task.Yield();
        throw new InvalidOperationException(Message);
    }
}
