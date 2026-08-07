namespace ZeroAlloc.Telemetry;

/// <summary>
/// Records the return value, or a member of it, as a tag on the span started by
/// <see cref="TraceAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// The tag is set after the wrapped call returns and before the span is disposed. For async
/// methods the value is taken from the awaited result, not the task.
/// </para>
/// <para>
/// Member access is null-safe: the generated code uses <c>?.</c>, so a null result records a null
/// tag rather than throwing. Instrumentation must not be able to fail a call that would otherwise
/// have succeeded.
/// </para>
/// <para>
/// Requires <see cref="TraceAttribute"/> on the same method, and a method that returns a value —
/// the generator reports <c>ZTEL004</c> or <c>ZTEL005</c> respectively rather than emitting a tag
/// that could never be set. May be applied more than once to record several members.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Trace("vectorstore.search")]
/// [TraceTagFromResult("vectorstore.result.count", "Count")]
/// Task&lt;IReadOnlyList&lt;SearchResult&gt;&gt; SearchAsync(string collection, CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class TraceTagFromResultAttribute : Attribute
{
    /// <summary>The tag key written to the span.</summary>
    public string Name { get; }

    /// <summary>
    /// Member of the result to record, e.g. <c>Count</c>. Null or empty records the result itself.
    /// Use <c>nameof</c> at the call site to keep this checked by the compiler.
    /// </summary>
    public string? Member { get; }

    /// <summary>Records the whole result under <paramref name="name"/>.</summary>
    /// <param name="name">The tag key.</param>
    public TraceTagFromResultAttribute(string name)
    {
        Name = name;
        Member = null;
    }

    /// <summary>Records <paramref name="member"/> of the result under <paramref name="name"/>.</summary>
    /// <param name="name">The tag key.</param>
    /// <param name="member">Member of the returned value to read, e.g. <c>Count</c>.</param>
    public TraceTagFromResultAttribute(string name, string member)
    {
        Name = name;
        Member = member;
    }
}
