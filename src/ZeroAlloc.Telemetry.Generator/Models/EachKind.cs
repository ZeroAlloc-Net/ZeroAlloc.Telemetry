namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>How a <c>[HistogramFromResult(Each = true)]</c> member is iterated.</summary>
internal enum EachKind
{
    /// <summary>Not per element: the member itself is recorded.</summary>
    None,

    /// <summary>A <c>ReadOnlySpan&lt;double&gt;</c> or <c>Span&lt;double&gt;</c>, handed to the proxy's static helper.</summary>
    Span,

    /// <summary>A <c>ReadOnlyMemory&lt;double&gt;</c> or <c>Memory&lt;double&gt;</c>, whose <c>Span</c> goes to the helper.</summary>
    Memory,

    /// <summary>An array or a type with a struct enumerator, iterated with <c>foreach</c> in place.</summary>
    Enumerable,
}
