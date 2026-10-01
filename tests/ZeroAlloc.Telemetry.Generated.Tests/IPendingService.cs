namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Every return shape the no-listener fast path covers (#169), each with a span and a metric, so
/// the fast path has to check both.
/// </summary>
[Instrument(PendingService.SourceName)]
public interface IPendingService
{
    [Trace("pending.task")]
    [Count("pending.task.calls")]
    Task RunAsync(int id);

    [Trace("pending.valuetask")]
    [Histogram("pending.valuetask.duration")]
    ValueTask StepAsync(int id);

    [Trace("pending.valuetask_of_t")]
    [CountFromResult("pending.values", "")]
    ValueTask<int> ReadAsync(int id);
}
