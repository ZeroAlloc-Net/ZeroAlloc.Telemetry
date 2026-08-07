namespace ZeroAlloc.Telemetry;

/// <summary>
/// Wraps the method body in a <see cref="System.Diagnostics.Activity"/> span.
/// The span is started before the call, stopped in a <c>finally</c>, and marked
/// <see cref="System.Diagnostics.ActivityStatusCode.Error"/> on exception.
/// </summary>
/// <remarks>
/// <para>
/// The attribute goes on the interface method, so by default every implementation of that
/// interface produces the same span name. Use the <c>{type}</c> token to distinguish them —
/// see <see cref="Name"/>.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TraceAttribute : Attribute
{
    /// <summary>
    /// The operation name passed to
    /// <see cref="System.Diagnostics.ActivitySource.StartActivity(string)"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// May contain the token <c>{type}</c>, which the generated proxy replaces with the wrapped
    /// implementation's type name. <c>"vectorstore.search.{type}"</c> yields
    /// <c>vectorstore.search.QdrantVectorStore</c> for one implementation and
    /// <c>vectorstore.search.WeaviateVectorStore</c> for another.
    /// </para>
    /// <para>
    /// This matters on an interface with several implementations, which is exactly where a span
    /// earns its keep: without it, a slow retrieval shows one span name and gives no way to tell
    /// which backend was the cost. It also replaces the common workaround of tagging
    /// <c>GetType().Name</c> by hand, putting the distinction in the span name where a trace UI
    /// groups on it.
    /// </para>
    /// <para>
    /// The substitution costs nothing per call. The wrapped instance cannot change for the
    /// lifetime of a proxy, so the name is composed once in the proxy's constructor and reused;
    /// the call path never concatenates a string.
    /// </para>
    /// <para>
    /// <c>{type}</c> is the only recognised token. Anything else in braces is emitted verbatim
    /// and reported as <c>ZTEL006</c>, rather than silently leaving a literal brace in a span
    /// name that only shows up on a dashboard later.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// [Instrument("ragnet")]
    /// public interface IVectorStore
    /// {
    ///     [Trace("vectorstore.search.{type}")]
    ///     Task&lt;IReadOnlyList&lt;SearchResult&gt;&gt; SearchAsync(string collection, CancellationToken ct);
    /// }
    /// </code>
    /// </example>
    public string Name { get; }

    /// <summary>Creates a span named <paramref name="name"/>.</summary>
    /// <param name="name">
    /// The span name. May contain <c>{type}</c> to substitute the implementation's type name.
    /// </param>
    public TraceAttribute(string name) => Name = name;
}
