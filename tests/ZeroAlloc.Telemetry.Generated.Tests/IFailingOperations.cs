namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Methods that throw, with the exception's message as the status description and without it, and
/// a histogram-only method, for the exception path's <c>error.type</c> (#184).
/// </summary>
[Instrument(FailingOperations.SourceName)]
public interface IFailingOperations
{
    [Trace("failing.described")]
    [Histogram("failing.described.duration")]
    Task DescribedAsync();

    [Trace("failing.quiet", ExceptionDescription = false)]
    Task QuietAsync();

    [Histogram("failing.untraced.duration")]
    int Untraced();
}
