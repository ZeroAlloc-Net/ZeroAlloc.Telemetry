namespace ZeroAlloc.Telemetry;

/// <summary>
/// Records an argument as a tag on the span started by <see cref="TraceAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// The tag is set immediately after the span starts, so it is present for the whole span and is
/// visible to samplers that inspect tags on <c>ActivityStarted</c>.
/// </para>
/// <para>
/// Tags cost nothing when nobody is listening. The generated call is
/// <c>_activity?.SetTag(...)</c>, and the null-conditional operator skips the entire invocation —
/// including evaluating and boxing the argument — when no listener sampled the span. Boxing of
/// value-typed arguments therefore only happens on spans that are actually recorded.
/// </para>
/// <para>
/// Requires <see cref="TraceAttribute"/> on the same method. Without a span there is nothing to
/// tag, so the generator reports <c>ZTEL004</c> rather than silently dropping the tag.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Trace("vectorstore.search")]
/// Task&lt;IReadOnlyList&lt;SearchResult&gt;&gt; SearchAsync(
///     [TraceTag("vectorstore.collection")] string collection,
///     [TraceTag("top.k")] int topK,
///     CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class TraceTagAttribute : Attribute
{
    /// <summary>The tag key written to the span.</summary>
    public string Name { get; }

    /// <summary>Creates a tag that records the decorated argument under <paramref name="name"/>.</summary>
    /// <param name="name">The tag key, e.g. <c>vectorstore.collection</c>.</param>
    public TraceTagAttribute(string name) => Name = name;
}
