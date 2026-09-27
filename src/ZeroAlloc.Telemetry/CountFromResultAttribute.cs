namespace ZeroAlloc.Telemetry;

/// <summary>
/// Adds a member of the return value to a <see cref="System.Diagnostics.Metrics.Counter{T}"/> of
/// <c>long</c> after a successful (non-throwing) call.
/// </summary>
/// <remarks>
/// <para>
/// For values only known once the call returns, such as tokens consumed, rows written or items
/// returned. The path is read from the awaited return value, exactly as
/// <see cref="TraceTagFromResultAttribute"/> reads it. On a <c>Result&lt;T, E&gt;</c>, the success
/// value is reached through <c>Value</c>, as in <c>Value.Usage.InputTokens</c>.
/// </para>
/// <para>
/// The member must convert implicitly to <c>long</c>: <c>sbyte</c>, <c>byte</c>, <c>short</c>,
/// <c>ushort</c>, <c>int</c>, <c>uint</c> or <c>long</c>, or a nullable form of one. Anything else
/// is reported as <c>ZTEL009</c>. A null value, or a null anywhere along the path, is not recorded.
/// </para>
/// <para>
/// A negative value is added as it is. Counters are expected to only increase, and a backend may
/// reject a decrease or report it as a reset, so count quantities that cannot go negative.
/// </para>
/// <para>
/// Does not require <see cref="TraceAttribute"/>: this is a metric, not span data. May be applied
/// more than once.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [CountFromResult("llm.tokens.input", "Value.Usage.InputTokens", When = "IsSuccess", Unit = "{token}")]
/// [CountFromResult("llm.tokens.output", "Value.Usage.OutputTokens", When = "IsSuccess", Unit = "{token}")]
/// ValueTask&lt;Result&lt;ChatResponse, ChatError&gt;&gt; CompleteAsync(ChatRequest request, CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CountFromResultAttribute : Attribute
{
    /// <summary>The metric name passed to <see cref="System.Diagnostics.Metrics.Meter.CreateCounter{T}(string, string?, string?)"/>.</summary>
    public string Metric { get; }

    /// <summary>
    /// Dotted path from the awaited return value to the member to add, e.g. <c>Value.Count</c>.
    /// Empty adds the return value itself.
    /// </summary>
    public string Member { get; }

    /// <summary>
    /// A dotted path to a <c>bool</c> or <c>bool?</c> member that must be true for the value to be
    /// added, e.g. <c>IsSuccess</c>. <see cref="Member"/> is not read otherwise. Null always adds.
    /// </summary>
    public string? When { get; set; }

    /// <summary>The instrument's unit, e.g. <c>{token}</c>. Null passes no unit.</summary>
    public string? Unit { get; set; }

    /// <summary>The instrument's description. Null passes none.</summary>
    public string? Description { get; set; }

    /// <summary>Adds <paramref name="member"/> of the return value to <paramref name="metric"/>.</summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="member">Dotted path from the awaited return value.</param>
    public CountFromResultAttribute(string metric, string member)
    {
        Metric = metric;
        Member = member;
    }
}
