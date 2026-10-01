using System.Diagnostics.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generated.Tests;

public sealed class ShapeService : IShapeService
{
    public const string SourceName = "ZeroAlloc.Telemetry.Generated.Tests.Shapes";

    public event EventHandler? Changed;

    public int Count { get; set; }

    public string this[int index] => "item" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);

    public int Peek() => 7;

    public bool TryGet(string key, [NotNullWhen(true)] out string? value)
    {
        value = string.Equals(key, "known", StringComparison.Ordinal) ? "found" : null;
        return value is not null;
    }

    public void Bump(ref int value, params int[] steps)
    {
        foreach (var step in steps)
            value += step;
    }

    /// <summary>Takes a span, so it cannot be async: it throws synchronously, or returns a task that completes later.</summary>
    public ValueTask<int> ParseAsync(ReadOnlySpan<char> text, out int consumed)
    {
        if (text.IsEmpty)
            throw new ArgumentException("empty", nameof(text));

        consumed = text.Length;
        return Later(text.Length * 10);
    }

    public void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    private static async ValueTask<int> Later(int value)
    {
        await Task.Delay(20).ConfigureAwait(false);
        return value;
    }
}
