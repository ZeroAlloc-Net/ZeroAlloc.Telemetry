using System.Diagnostics.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>Every member shape #173 made the proxy support, through the real generator.</summary>
[Instrument(ShapeService.SourceName)]
public interface IShapeService : IShapeReader
{
    [Trace("shape.tryget")]
    [Count("shape.tryget.calls")]
    bool TryGet(string key, [NotNullWhen(true)] [MetricTag("shape.value")] out string? value);

    [Trace("shape.bump")]
    void Bump(ref int value, params int[] steps);

    [Trace("shape.parse")]
    [Histogram("shape.parse.duration")]
    ValueTask<int> ParseAsync(ReadOnlySpan<char> text, out int consumed);

    int Count { get; set; }

    string this[int index] { get; }

    event EventHandler? Changed;
}
