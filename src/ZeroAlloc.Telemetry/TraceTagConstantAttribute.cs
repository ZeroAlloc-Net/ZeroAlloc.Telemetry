namespace ZeroAlloc.Telemetry;

/// <summary>
/// Records a compile-time constant as a tag on the span started by <see cref="TraceAttribute"/>.
/// </summary>
/// <remarks>
/// <para>
/// For values that are simply known rather than derived from an argument or a return value —
/// which implementation ran, which mode, which provider. When several types implement one
/// <see cref="InstrumentAttribute"/>-annotated interface, this is how their spans are told apart.
/// </para>
/// <para>
/// OpenTelemetry's semantic conventions require a number of constant-valued attributes, such as
/// <c>gen_ai.operation.name</c>, <c>gen_ai.provider.name</c> and <c>db.system.name</c>. Those have
/// no other way to be expressed here.
/// </para>
/// <para>
/// The value is any constant an attribute can carry — string, bool, numeric, or enum. It is
/// emitted as a literal, so nothing is evaluated at run time and the tag costs a
/// <c>SetTag</c> call on a sampled span and nothing at all otherwise.
/// </para>
/// <para>
/// Requires <see cref="TraceAttribute"/> on the same method. Without a span there is nothing to
/// tag, so the generator reports <c>ZTEL004</c> rather than silently dropping the tag. May be
/// applied more than once.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Trace("rerank.run")]
/// [TraceTagConstant("reranker.type", "CohereReranker")]
/// [TraceTagConstant("gen_ai.operation.name", "chat")]
/// Task&lt;IReadOnlyList&lt;RerankResult&gt;&gt; RerankAsync(CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class TraceTagConstantAttribute : Attribute
{
    /// <summary>The tag key written to the span.</summary>
    public string Name { get; }

    /// <summary>The constant value recorded under <see cref="Name"/>.</summary>
    public object? Value { get; }

    /// <summary>Records <paramref name="value"/> under <paramref name="name"/>.</summary>
    /// <param name="name">The tag key, e.g. <c>reranker.type</c>.</param>
    /// <param name="value">
    /// A constant the attribute can carry — string, bool, numeric, or enum.
    /// </param>
    public TraceTagConstantAttribute(string name, object? value)
    {
        Name = name;
        Value = value;
    }
}
