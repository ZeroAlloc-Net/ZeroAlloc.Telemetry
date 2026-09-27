namespace ZeroAlloc.Telemetry.Generated.Tests;

public sealed class FakeChatService : IChatService
{
    public ChatResult Result { get; init; } = ChatResult.Failure();

    public double? Score { get; init; }

    public bool Throw { get; init; }

    public Task<ChatResult> CompleteAsync(string prompt, CancellationToken ct) =>
        Throw
            ? Task.FromException<ChatResult>(new InvalidOperationException("boom"))
            : Task.FromResult(Result);

    public Task<double?> ScoreAsync(CancellationToken ct) => Task.FromResult(Score);

    public Task PingAsync(CancellationToken ct) =>
        Throw
            ? Task.FromException(new InvalidOperationException("boom"))
            : Task.CompletedTask;

    public Task<int> RetryCountAsync(CancellationToken ct) => Task.FromResult(3);
}
