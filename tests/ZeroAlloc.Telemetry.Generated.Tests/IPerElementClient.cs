namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>One measurement per backend call, tagged with the model that call used.</summary>
[Instrument(PerElementClient.MeterName)]
public interface IPerElementClient
{
    [HistogramFromResult("elem.token.usage", "Calls", Each = true, ElementMember = "InputTokens", Unit = "{token}")]
    [CountFromResult("elem.tokens.output", "Calls", Each = true, ElementMember = "OutputTokens", Unit = "{token}")]
    [MetricTagFromResult("elem.response.model", "Model", FromElement = true, Metric = "elem.token.usage")]
    [MetricTagConstant("elem.token.type", "input", Metric = "elem.token.usage")]
    [MetricTagFromResult("elem.result.size", "Calls.Length")]
    PerElementResponse Evaluate();
}
