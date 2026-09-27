namespace ZeroAlloc.Telemetry.Generated.Tests;

// The name is a literal because attribute arguments on a type are bound outside that type, so
// a constant declared inside the interface is not in scope here. ResultMetricsBehaviorTests
// repeats it as its MeterName.
[Instrument("ZeroAlloc.Telemetry.Generated.Tests.Chat")]
public interface IChatService
{
    [Count("chat.completions", When = "IsSuccess", Unit = "{completion}", Description = "Successful completions")]
    [Histogram("chat.duration", When = "IsSuccess", Unit = "ms")]
    [CountFromResult("chat.tokens.input", "Value.Input", When = "IsSuccess", Unit = "{token}", Description = "Prompt tokens consumed")]
    [CountFromResult("chat.tokens.output", "Value.Output", When = "IsSuccess", Unit = "{token}")]
    [HistogramFromResult("chat.cost", "Value.Cost", When = "IsSuccess", Unit = "USD")]
    Task<ChatResult> CompleteAsync(string prompt, CancellationToken ct);

    [HistogramFromResult("chat.score", "Value")]
    Task<double?> ScoreAsync(CancellationToken ct);

    // Unguarded: no When, so the histogram records on both the success and the throw path.
    [Histogram("chat.raw_duration")]
    Task PingAsync(CancellationToken ct);

    // No [Trace]: proves a result metric does not need a span to work.
    [CountFromResult("chat.retries", "")]
    Task<int> RetryCountAsync(CancellationToken ct);
}
