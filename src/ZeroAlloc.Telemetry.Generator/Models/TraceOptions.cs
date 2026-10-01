namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>
/// What a <c>[Trace]</c> asks for beyond a plain span. Null on the method model when it asks for
/// none of it, so a plain span is emitted exactly as before.
/// </summary>
/// <param name="Kind">
/// The span kind as a C# expression, such as <c>ActivityKind.Client</c>; null for the default,
/// <c>Internal</c>.
/// </param>
/// <param name="ErrorGuard">
/// The resolved <c>ErrorWhen</c> condition to append to the result root, such as
/// <c>.IsFailure</c>; null when the status is left to exceptions.
/// </param>
/// <param name="ErrorDescription">
/// The resolved <c>ErrorDescription</c> to append to the result root, converted to a string; null
/// for no description.
/// </param>
/// <param name="DisplayName">
/// The expression composing the full span name from parameter tokens; null when the name has none.
/// </param>
/// <param name="DisplayNameCopies">
/// The arguments the display name reads through a null-conditional, each as its copy's name and
/// the argument, so testing the copy leaves the forwarded argument's null-state alone.
/// </param>
/// <param name="TagsAtStart">Whether the constant and parameter tags go to <c>StartActivity</c>.</param>
internal sealed record TraceOptions(
    string? Kind,
    string? ErrorGuard,
    string? ErrorDescription,
    string? DisplayName,
    EquatableArray<string> DisplayNameCopies,
    bool TagsAtStart);
