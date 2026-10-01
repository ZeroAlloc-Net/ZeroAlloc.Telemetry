namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Awaitable methods whose inner implementation fails in a chosen way, with and without
/// instruments, so the proxy's exception semantics can be compared with and without a listener.
/// </summary>
[Instrument(ThrowingService.SourceName)]
public interface IThrowingService
{
    [Trace("throwing.task")]
    [Count("throwing.task.calls")]
    Task RunAsync();

    [Trace("throwing.valuetask")]
    ValueTask StepAsync();

    [Histogram("throwing.read.duration")]
    ValueTask<int> ReadAsync();

    Task<int> PlainAsync();
}
