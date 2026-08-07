namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="ResultCanBeNull">
/// Whether the value the tag reads from can be null — a reference type or <c>Nullable&lt;T&gt;</c>,
/// after unwrapping <c>Task&lt;T&gt;</c>/<c>ValueTask&lt;T&gt;</c>. Drives whether member access on
/// the result is emitted as <c>?.</c> or <c>.</c>; <c>?.</c> on a non-nullable value type does not
/// compile.
/// </param>
/// <param name="TraceNameExpression">
/// A C# expression composing the span name from the wrapped instance's type, when
/// <see cref="TraceName"/> contains the <c>{type}</c> token — e.g. <c>"search." + _implName</c>.
/// Null when the name is a constant, which is the common case. The proxy evaluates this once in
/// its constructor and caches the result, so the per-call path never composes a string.
/// </param>
internal sealed record MethodModel(
    string Name,
    string ReturnType,
    bool IsAsync,
    bool ReturnsVoid,
    IReadOnlyList<ParameterModel> Parameters,
    string? TraceName,
    string? CountMetric,
    string? HistogramMetric,
    IReadOnlyList<ResultTagModel> ResultTags,
    bool ResultCanBeNull,
    IReadOnlyList<ConstantTagModel> ConstantTags,
    string? TraceNameExpression = null
);
