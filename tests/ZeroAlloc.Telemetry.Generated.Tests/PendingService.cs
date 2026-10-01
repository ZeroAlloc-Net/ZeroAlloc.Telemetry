namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Completes asynchronously: every call returns a task that is still pending, built once, so the
/// inner call itself allocates nothing and anything allocated during a call is the proxy's.
/// </summary>
public sealed class PendingService : IPendingService
{
    public const string SourceName = "ZeroAlloc.Telemetry.Generated.Tests.Pending";

    private readonly TaskCompletionSource<int> _pending = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task RunAsync(int id) => _pending.Task;

    public ValueTask StepAsync(int id) => new(_pending.Task);

    public ValueTask<int> ReadAsync(int id) => new(_pending.Task);

    /// <summary>Completes every call made so far, and every later one, with <paramref name="value"/>.</summary>
    public void Complete(int value) => _pending.SetResult(value);
}
