namespace ZeroAlloc.Telemetry.Generator.Models;

/// <param name="HintName">
/// The name of the generated file, from <see cref="HintNames.ForInterface"/>. Computed while
/// parsing, since emitting has no symbols, and kept as a string so the model stays equatable.
/// </param>
/// <param name="InterfaceName">
/// The interface as the proxy refers to it: its name, with its type parameters when it is generic.
/// </param>
/// <param name="ProxyName">The proxy's simple name, without type parameters.</param>
/// <param name="TypeParameters">The proxy's type parameter list, such as <c>&lt;T&gt;</c>, or empty.</param>
/// <param name="ConstraintClauses">The <c>where</c> clauses the proxy repeats from the interface.</param>
/// <param name="Version">
/// The <c>Version</c> set on <c>[Instrument]</c>, or null when it is not set and the assembly's
/// informational version applies.
/// </param>
/// <param name="Properties">The interface's properties, indexers and events, which the proxy forwards.</param>
/// <param name="ContainingTypes">
/// The partial declarations of the interface's containing types, outermost first, which the
/// proxy is emitted inside; empty for a top-level interface.
/// </param>
internal sealed record InstrumentModel(
    string HintName,
    string? Namespace,
    string InterfaceName,
    string ProxyName,
    string ActivitySourceName,
    EquatableArray<MethodModel> Methods,
    bool PublicProxy,
    string TypeParameters,
    EquatableArray<string> ConstraintClauses,
    EquatableArray<string> ContainingTypes,
    string? Version = null,
    EquatableArray<PropertyModel> Properties = default
);
