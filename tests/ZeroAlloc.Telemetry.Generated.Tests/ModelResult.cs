namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// A Result-shaped struct whose <see cref="Value"/> throws on failure, so a tag read on a failed
/// result would fail the call. A struct, returned by a synchronous method, so a call through the
/// proxy allocates nothing of its own and the allocation test measures the proxy alone.
/// </summary>
public readonly struct ModelResult
{
    private readonly ModelReply? _value;

    private ModelResult(bool isSuccess, ModelReply? value)
    {
        IsSuccess = isSuccess;
        _value = value;
    }

    public bool IsSuccess { get; }

    public ModelReply Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static ModelResult Success(ModelReply value) => new(isSuccess: true, value);

    public static ModelResult Failure() => new(isSuccess: false, value: null);
}
