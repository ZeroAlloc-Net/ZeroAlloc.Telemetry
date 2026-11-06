namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>One backend call of a logical call: its model and the tokens it used.</summary>
public sealed record BackendCall(string? Model, long? InputTokens, long? OutputTokens);
