namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Methods that end in an <see cref="OperationCanceledException"/>, for telling a caller's
/// cancellation from an error (#233).
/// </summary>
[Instrument(CancellableOperations.SourceName)]
public interface ICancellableOperations
{
    [Trace("cancellable.run")]
    [Histogram("cancellable.run.duration")]
    ValueTask<int> RunAsync(CancellationToken ct);

    [Trace("cancellable.strict", CancellationIsError = true)]
    [Histogram("cancellable.strict.duration")]
    Task StrictAsync(CancellationToken ct);

    [Histogram("cancellable.sync.duration")]
    int Run(CancellationToken ct);

    [Trace("cancellable.split")]
    Task SplitAsync(in int value, CancellationToken ct);
}
