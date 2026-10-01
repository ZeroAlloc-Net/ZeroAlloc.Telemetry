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
/// <param name="ElapsedMember">
/// For <c>[Histogram]</c>, the <c>TimeSpan</c> property the elapsed time is read from, chosen by
/// its unit, such as <c>TotalSeconds</c>.
/// </param>
/// <param name="Buckets">
/// For a histogram, the bucket boundaries as C# literals, passed as instrument advice; empty for none.
/// </param>
/// <param name="Each">
/// For <c>[HistogramFromResult(Each = true)]</c>, how the member is iterated; otherwise
/// <see cref="EachKind.None"/>.
/// </param>
/// <param name="ElementCanBeNull">For <see cref="EachKind.Enumerable"/>, whether an element can be null and is skipped.</param>
/// <param name="ElementIsDecimal">For <see cref="EachKind.Enumerable"/>, whether an element is cast from decimal.</param>
internal sealed record MetricModel(
    MetricKind Kind,
    string Metric,
    string? Unit = null,
    string? Description = null,
    string? GuardExpression = null,
    string? ValueAccess = null,
    bool ValueCanBeNull = false,
    bool ValueIsDecimal = false,
    string ElapsedMember = "TotalMilliseconds",
    EquatableArray<string> Buckets = default,
    EachKind Each = EachKind.None,
    bool ElementCanBeNull = false,
    bool ElementIsDecimal = false);
