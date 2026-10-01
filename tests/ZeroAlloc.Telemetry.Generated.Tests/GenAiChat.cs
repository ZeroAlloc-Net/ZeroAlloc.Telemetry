namespace ZeroAlloc.Telemetry.Generated.Tests;

public sealed class GenAiChat : IGenAiChat
{
    public const string SourceName = "ZeroAlloc.Telemetry.Generated.Tests.GenAiChat";

    public async ValueTask<GenAiChatResult> ChatAsync(string operation, GenAiRequest request, CancellationToken ct)
    {
        await Task.Yield();
        return request.Fail
            ? new GenAiChatResult { IsFailure = true, Error = "rate limited" }
            : new GenAiChatResult { ResponseModel = request.Model + "-2026-09" };
    }

    public int Embed(string model) => model.Length;

    public int EmbedPlain(string model) => model.Length;
}
