namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="ResultCanBeNull">
/// Whether the value the tag reads from can be null — a reference type or <c>Nullable&lt;T&gt;</c>,
/// after unwrapping <c>Task&lt;T&gt;</c>/<c>ValueTask&lt;T&gt;</c>. Drives whether member access on
/// the result is emitted as <c>?.</c> or <c>.</c>; <c>?.</c> on a non-nullable value type does not
/// compile.
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
    IReadOnlyList<ConstantTagModel> ConstantTags
);
