namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>Several span tags from one parameter (#181), set after the span starts and at its start.</summary>
[Instrument(EndpointService.SourceName)]
public interface IEndpointService
{
    [Trace("endpoint.ask")]
    ValueTask<int> AskAsync(
        [TraceTag("server.address", "Host")] [TraceTag("server.port", "Port")] Uri endpoint,
        [TraceTag("gen_ai.request.model", "Model")] [TraceTag("jev.question.count", "Questions.Count")] AskRequest request);

    [Trace("endpoint.sampled", TagsAtStart = true)]
    ValueTask<int> SampledAsync(
        [TraceTag("server.address", "Host")] [TraceTag("server.port", "Port")] Uri endpoint,
        [TraceTag("gen_ai.request.model", "Model")] [TraceTag("jev.question.count", "Questions.Count")] AskRequest request);
}
