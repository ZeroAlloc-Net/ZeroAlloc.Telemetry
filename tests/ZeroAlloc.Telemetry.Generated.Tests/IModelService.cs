namespace ZeroAlloc.Telemetry.Generated.Tests;

// The name is a literal for the reason given on IChatService. ModelMeterCollection repeats it.
[Instrument("ZeroAlloc.Telemetry.Generated.Tests.Model")]
public interface IModelService
{
    // The model tag goes on every metric; the shard tag, an int that would box, only on
    // model.tokens. [Count] and [Histogram] have no When, so a failed result is still recorded,
    // without the guarded tags.
    [Count("model.calls")]
    [Histogram("model.duration")]
    [CountFromResult("model.tokens", "Value.Tokens", When = "IsSuccess")]
    [MetricTagFromResult("gen_ai.response.model", "Value.Model", When = "IsSuccess")]
    [MetricTagFromResult("model.shard", "Value.Shard", When = "IsSuccess", Metric = "model.tokens")]
    ModelResult Complete(string prompt);

    // Unguarded histogram: it also records when the call throws, and then carries no tag.
    [Histogram("model.ping")]
    [MetricTagFromResult("gen_ai.response.model", "Model")]
    Task<ModelReply> PingAsync(CancellationToken ct);
}
