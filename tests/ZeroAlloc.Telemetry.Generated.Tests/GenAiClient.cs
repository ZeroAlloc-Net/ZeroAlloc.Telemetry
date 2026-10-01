namespace ZeroAlloc.Telemetry.Generated.Tests;

public sealed class GenAiClient : IGenAiClient
{
    public const string MeterName = "ZeroAlloc.Telemetry.Generated.Tests.GenAi";

    private static readonly double[] Tokens = [12, 34];

    public async ValueTask<GenAiUsage> ChatAsync(GenAiRequest request, CancellationToken ct)
    {
        await Task.Delay(30, ct).ConfigureAwait(false);
        if (request.Fail)
            throw new InvalidOperationException("boom");

        return new GenAiUsage { Tokens = Tokens };
    }

    public GenAiUsage Measure(string model) => new() { Tokens = Tokens };
}
