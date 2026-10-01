namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="ResultCanBeNull">
/// Whether the value the tag reads from can be null — a reference type or <c>Nullable&lt;T&gt;</c>,
/// after unwrapping the awaited type of <c>Task&lt;T&gt;</c>, <c>ValueTask&lt;T&gt;</c> or a task-like type. Drives whether member access on
/// the result is emitted as <c>?.</c> or <c>.</c>; <c>?.</c> on a non-nullable value type does not
/// compile.
/// </param>
/// <param name="TraceNameExpression">
/// A C# expression composing the span name from the wrapped instance's type, when
/// <see cref="TraceName"/> contains the <c>{type}</c> token — e.g. <c>"search." + _implName</c>.
/// Null when the name is a constant, which is the common case. The proxy evaluates this once in
/// its constructor and caches the result, so the per-call path never composes a string.
/// </param>
/// <param name="Count"><c>[Count]</c> on the method, or null.</param>
/// <param name="Histogram"><c>[Histogram]</c> on the method, or null.</param>
/// <param name="ResultMetrics">
/// <c>[CountFromResult]</c> and <c>[HistogramFromResult]</c> uses, in attribute order. Only the
/// ones that resolved and type-checked; the rest were reported and are not emitted.
/// </param>
/// <param name="MetricTags">
/// <c>[MetricTagFromResult]</c> uses, in attribute order. Only the ones that resolved and apply
/// to a metric the method declares; the rest were reported and are not emitted.
/// </param>
/// <param name="TypeParameters">
/// The method's type parameter list, such as <c>&lt;T&gt;</c>, or empty for a non-generic method.
/// </param>
/// <param name="ConstraintClauses">The <c>where</c> clauses the proxy method repeats from the interface method.</param>
/// <param name="Trace">What <c>[Trace]</c> asks for beyond a plain span, or null for nothing more.</param>
/// <param name="RefReturn">
/// <c>ref </c> or <c>ref readonly </c> for a method returning by reference, which is forwarded
/// without instrumentation; empty otherwise.
/// </param>
/// <param name="Receiver">
/// What the call is forwarded to: <c>_inner</c>, or for a member inherited from another
/// interface, <c>_inner</c> cast to it, so a member declared by two of them is not ambiguous.
/// </param>
/// <param name="ConfigureAwait">
/// Whether the awaited inner call has <c>ConfigureAwait(bool)</c>: <c>Task</c> and <c>ValueTask</c>
/// do, and a task-like type may.
/// </param>
internal sealed record MethodModel(
    string Name,
    string ReturnType,
    bool IsAsync,
    bool ReturnsVoid,
    EquatableArray<ParameterModel> Parameters,
    string? TraceName,
    MetricModel? Count,
    MetricModel? Histogram,
    EquatableArray<ResultTagModel> ResultTags,
    bool ResultCanBeNull,
    EquatableArray<ConstantTagModel> ConstantTags,
    EquatableArray<MetricModel> ResultMetrics,
    EquatableArray<MetricTagModel> MetricTags,
    string? TraceNameExpression = null,
    string TypeParameters = "",
    EquatableArray<string> ConstraintClauses = default,
    TraceOptions? Trace = null,
    bool ConfigureAwait = false,
    string RefReturn = "",
    string Receiver = "_inner"
)
{
    /// <summary>
    /// An awaitable method whose parameters an async method cannot take: <c>ref</c>, <c>out</c>,
    /// <c>in</c> or a ref struct. The proxy calls the inner method synchronously, while the
    /// parameters are still in scope, and hands the awaitable to the async core.
    /// </summary>
    public bool SplitCall => IsAsync && Parameters.Any(static p => !p.FitsAsync);
}
