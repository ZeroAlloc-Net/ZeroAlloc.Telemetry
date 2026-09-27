namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>One use of an instrument by one method.</summary>
/// <param name="Kind">Which instrument, and so which field type.</param>
/// <param name="Metric">The metric name passed to the <c>Meter</c> factory.</param>
/// <param name="Unit">Passed to the factory as <c>unit:</c>; null passes none.</param>
/// <param name="Description">Passed to the factory as <c>description:</c>; null passes none.</param>
/// <param name="GuardExpression">
/// The resolved <c>When</c> condition to append to the result root, such as
/// <c>?.IsSuccess == true</c>. Null records unconditionally.
/// </param>
/// <param name="ValueAccess">
/// For <c>[CountFromResult]</c>/<c>[HistogramFromResult]</c>, the resolved member access to append
/// to the result root; empty reads the root itself. Null for <c>[Count]</c>, which adds 1, and for
/// <c>[Histogram]</c>, which records elapsed milliseconds.
/// </param>
/// <param name="ValueCanBeNull">
/// Whether the value expression can be null. It is then read with <c>is { } _readN</c>, which
/// skips a null and gives a non-nullable local.
/// </param>
/// <param name="ValueIsDecimal">
/// <c>decimal</c> or <c>decimal?</c>, which has no implicit conversion to <c>double</c> and is cast.
/// </param>
internal sealed record MetricModel(
    MetricKind Kind,
    string Metric,
    string? Unit = null,
    string? Description = null,
    string? GuardExpression = null,
    string? ValueAccess = null,
    bool ValueCanBeNull = false,
    bool ValueIsDecimal = false);
