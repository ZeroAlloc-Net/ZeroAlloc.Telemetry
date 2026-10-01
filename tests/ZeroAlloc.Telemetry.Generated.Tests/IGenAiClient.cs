namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>The OpenTelemetry GenAI client metrics, as ZeroAlloc.Jev declares them (#171).</summary>
[Instrument(GenAiClient.MeterName)]
public interface IGenAiClient
{
    [Histogram("gen_ai.client.operation.duration", Unit = "s", Buckets = new[] { 0.01, 0.02, 0.04, 0.08, 0.16, 0.32, 0.64, 1.28, 2.56, 5.12 })]
    [HistogramFromResult("gen_ai.client.token.usage", "Tokens", Each = true, Unit = "{token}", Buckets = new[] { 1d, 4d, 16d, 64d })]
    [MetricTagConstant("gen_ai.operation.name", "chat")]
    [MetricTagConstant("gen_ai.provider.name", "openai")]
    [MetricTagConstant("gen_ai.token.modality", "text", Metric = "gen_ai.client.token.usage")]
    ValueTask<GenAiUsage> ChatAsync([MetricTag("gen_ai.request.model", "Model")] GenAiRequest request, CancellationToken ct);

    [HistogramFromResult("gen_ai.sync.token.usage", "Tokens", Each = true)]
    [MetricTagConstant("gen_ai.operation.name", "embeddings")]
    GenAiUsage Measure([MetricTag("gen_ai.request.model")] string model);
}
