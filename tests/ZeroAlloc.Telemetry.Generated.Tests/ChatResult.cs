namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// A Result-shaped struct whose <see cref="Value"/> throws on failure. If the proxy read a guarded
/// member on a failed result, the call itself would throw, so these tests prove the guard stops
/// the read, not merely the recording.
/// </summary>
public readonly struct ChatResult
{
    private readonly TokenUsage? _value;

    private ChatResult(bool isSuccess, TokenUsage? value)
    {
        IsSuccess = isSuccess;
        _value = value;
    }

    public bool IsSuccess { get; }

    public TokenUsage Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static ChatResult Success(TokenUsage value) => new(isSuccess: true, value);

    public static ChatResult Failure() => new(isSuccess: false, value: null);
}
