namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Throws the caller's cancellation when the token is cancelled, and otherwise a cancellation of
/// its own, as a timeout inside the implementation would.
/// </summary>
public sealed class CancellableOperations : ICancellableOperations
{
    public const string SourceName = "ZeroAlloc.Telemetry.Generated.Tests.Cancellable";

    public async ValueTask<int> RunAsync(CancellationToken ct)
    {
        await Task.Yield();
        Cancel(ct);
        return 0;
    }

    public async Task StrictAsync(CancellationToken ct)
    {
        await Task.Yield();
        Cancel(ct);
    }

    public int Run(CancellationToken ct)
    {
        Cancel(ct);
        return 0;
    }

    // Throws before it returns a task, so the proxy's synchronous catch sees it.
    public Task SplitAsync(in int value, CancellationToken ct)
    {
        Cancel(ct);
        return Task.CompletedTask;
    }

    private static void Cancel(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        throw new OperationCanceledException("an internal timeout");
    }
}
