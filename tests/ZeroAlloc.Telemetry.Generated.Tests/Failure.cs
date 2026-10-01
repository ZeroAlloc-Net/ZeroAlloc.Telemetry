namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>How <see cref="ThrowingService"/> fails.</summary>
public enum Failure
{
    /// <summary>Throws before returning an awaitable.</summary>
    SynchronousThrow,

    /// <summary>Throws an <see cref="OperationCanceledException"/> before returning an awaitable.</summary>
    SynchronousCancel,

    /// <summary>Returns an awaitable that faults later.</summary>
    AsynchronousThrow,
}
