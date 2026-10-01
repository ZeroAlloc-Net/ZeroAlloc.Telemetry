namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>A minimal result struct in the shape ZeroAlloc.Jev returns.</summary>
internal readonly struct Result<T, E>
{
    private Result(T value, E? error, bool isFailure)
    {
        Value = value;
        Error = error;
        IsFailure = isFailure;
    }

    public T Value { get; }

    public E? Error { get; }

    public bool IsFailure { get; }

    public static Result<T, E> Success(T value) => new(value, default, isFailure: false);

    public static Result<T, E> Failure(E error) => new(default!, error, isFailure: true);
}
