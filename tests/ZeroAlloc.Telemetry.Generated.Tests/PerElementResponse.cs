using System.Collections.Immutable;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>A response whose usage is spread over several backend calls (#241).</summary>
public sealed record PerElementResponse(ImmutableArray<BackendCall?> Calls);
