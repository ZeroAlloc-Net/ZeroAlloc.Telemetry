namespace ZeroAlloc.Telemetry.Generator.Models;

internal sealed record MethodModel(
    string Name,
    string ReturnType,
    bool IsAsync,
    bool ReturnsVoid,
    IReadOnlyList<ParameterModel> Parameters,
    string? TraceName,
    string? CountMetric,
    string? HistogramMetric
);
