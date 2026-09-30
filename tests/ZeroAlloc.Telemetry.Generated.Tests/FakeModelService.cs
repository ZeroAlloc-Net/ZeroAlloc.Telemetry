namespace ZeroAlloc.Telemetry.Generated.Tests;

public sealed class FakeModelService : IModelService
{
    public ModelResult Result { get; init; } = ModelResult.Failure();

    public bool Throw { get; init; }

    public ModelResult Complete(string prompt) => Result;

    public Task<ModelReply> PingAsync(CancellationToken ct) =>
        Throw
            ? Task.FromException<ModelReply>(new InvalidOperationException("boom"))
            : Task.FromResult(Result.Value);
}
