using System.Diagnostics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>The OpenTelemetry GenAI client span, as ZeroAlloc.Jev declares it (#170).</summary>
[Instrument(GenAiChat.SourceName)]
public interface IGenAiChat
{
    [Trace("{operation} {request.Model}", Kind = ActivityKind.Client, ErrorWhen = "IsFailure", ErrorDescription = "Error", TagsAtStart = true)]
    [TraceTagConstant("gen_ai.provider.name", "openai")]
    [TraceTagFromResult("gen_ai.response.model", "ResponseModel")]
    ValueTask<GenAiChatResult> ChatAsync(
        [TraceTag("gen_ai.operation.name")] string operation,
        [TraceTag("gen_ai.request.model", "Model")] GenAiRequest request,
        CancellationToken ct);

    [Trace("embeddings {model}")]
    int Embed(string model);

    [Trace("embeddings")]
    int EmbedPlain(string model);
}
