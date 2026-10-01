namespace ZeroAlloc.Telemetry.Generated.Tests;

public sealed class EndpointService : IEndpointService
{
    public const string SourceName = "ZeroAlloc.Telemetry.Generated.Tests.Endpoint";

    public ValueTask<int> AskAsync(Uri endpoint, AskRequest request) => new(request.Questions.Count);

    public ValueTask<int> SampledAsync(Uri endpoint, AskRequest request) => new(request.Questions.Count);
}
