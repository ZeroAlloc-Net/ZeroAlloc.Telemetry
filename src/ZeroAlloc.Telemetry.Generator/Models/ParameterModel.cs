namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="Type">Fully-qualified parameter type.</param>
/// <param name="Name">Parameter name, used both in the signature and in the forwarded call.</param>
/// <param name="Tags">
/// The <c>[TraceTag]</c> tags recorded from the parameter, in attribute order; empty when it is
/// not recorded on the span.
/// </param>
/// <param name="Modifier">
/// The modifiers in the declaration, such as <c>ref </c>, <c>scoped </c> or <c>params </c>, with
/// the parameter's nullability attributes before them; empty for a plain parameter.
/// </param>
/// <param name="ArgumentModifier">The modifier in a forwarded call: <c>ref </c>, <c>out </c>, <c>in </c> or empty.</param>
/// <param name="IsOut">An <c>out</c> parameter, which has no value until the call returns.</param>
/// <param name="IsRefLike">A ref struct, which cannot be boxed or live in an async method.</param>
internal sealed record ParameterModel(
    string Type,
    string Name,
    EquatableArray<TraceTagModel> Tags = default,
    string Modifier = "",
    string ArgumentModifier = "",
    bool IsOut = false,
    bool IsRefLike = false)
{
    /// <summary>Whether an async method can take the parameter: no modifier that makes it a reference, and not a ref struct.</summary>
    public bool FitsAsync => ArgumentModifier.Length == 0 && !IsRefLike;
}
