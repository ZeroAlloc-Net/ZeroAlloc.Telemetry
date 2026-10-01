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
    /// May also contain <c>{parameter}</c> or <c>{parameter.Member}</c> tokens, which take the
    /// value of an argument, as in <c>"{operation} {model}"</c> for the OpenTelemetry GenAI span
    /// name. The span starts under the name with those tokens left out, or the method name when
    /// nothing is left, and its <c>DisplayName</c> is set to the full name only when the span was
    /// sampled, so an unsampled call builds no string. Values are formatted with the invariant
    /// culture, and a null value is empty. A member path that does not resolve is reported as
    /// <c>ZTEL022</c> and the token is left out. <c>{type}</c> keeps its meaning even when a
    /// parameter is named <c>type</c>.
    /// </para>
    /// <para>
    /// Any other token in braces is emitted verbatim and reported as <c>ZTEL006</c>, rather than
    /// silently leaving a literal brace in a span name that only shows up on a dashboard later.
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

    /// <summary>
    /// The span's kind, such as <see cref="System.Diagnostics.ActivityKind.Client"/> for a call to
    /// a remote service. Defaults to <see cref="System.Diagnostics.ActivityKind.Internal"/>.
    /// </summary>
    public System.Diagnostics.ActivityKind Kind { get; set; }

    /// <summary>
    /// A dotted path from the awaited return value to a <c>bool</c> or <c>bool?</c> member that,
    /// when true, sets the span's status to <see cref="System.Diagnostics.ActivityStatusCode.Error"/>,
    /// such as <c>IsFailure</c> on a <c>Result</c>. Null leaves the status to exceptions alone.
    /// </summary>
    /// <remarks>
    /// Resolved like <see cref="TraceTagFromResultAttribute.When"/>: a path that does not resolve
    /// is <c>ZTEL007</c>, one that is not a bool is <c>ZTEL008</c>, and on a method with no result
    /// <c>ZTEL005</c>. Only read when the span was sampled.
    /// </remarks>
    public string? ErrorWhen { get; set; }

    /// <summary>
    /// A dotted path from the awaited return value to the status description used when
    /// <see cref="ErrorWhen"/> is true, such as <c>Error.Message</c>. A member that is not a string
    /// is converted with <c>ToString()</c>. Null sets no description. Only read on an error.
    /// </summary>
    public string? ErrorDescription { get; set; }

    /// <summary>
    /// Passes the <see cref="TraceTagConstantAttribute"/> and <see cref="TraceTagAttribute"/> tags
    /// to <c>StartActivity</c>, so a sampler sees them when it decides, as the OpenTelemetry GenAI
    /// conventions recommend. False sets them on the span after it starts.
    /// </summary>
    /// <remarks>
    /// The tags are only collected when the source has a listener. They are then boxed into one
    /// <c>TagList</c> per call, sampled or not, because <c>StartActivity</c> takes them as an
    /// enumerable; with no listener nothing is allocated.
    /// </remarks>
    public bool TagsAtStart { get; set; }

    /// <summary>Creates a span named <paramref name="name"/>.</summary>
    /// <param name="name">
    /// The span name. May contain <c>{type}</c> to substitute the implementation's type name.
    /// </param>
    public TraceAttribute(string name) => Name = name;
}
