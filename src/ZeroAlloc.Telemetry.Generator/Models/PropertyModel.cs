namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>
/// A property, indexer or event of the interface. The proxy forwards it to the wrapped instance
/// without instrumentation, so the proxy implements the whole interface.
/// </summary>
/// <param name="Kind">Property, indexer or event.</param>
/// <param name="Type">The fully-qualified member type, with its nullable annotation.</param>
/// <param name="Name">The member name; unused for an indexer.</param>
/// <param name="Parameters">An indexer's parameters; empty otherwise.</param>
/// <param name="HasGetter">Whether a property or indexer has a getter.</param>
/// <param name="HasSetter">Whether a property or indexer has a setter.</param>
/// <param name="InitOnly">
/// Whether the setter is <c>init</c>. An init accessor can only run in an object initializer of
/// the proxy itself, so it cannot be forwarded and throws instead.
/// </param>
/// <param name="Receiver">What the member is forwarded to; see <see cref="MethodModel.Receiver"/>.</param>
/// <param name="RefPrefix">
/// <c>ref </c> or <c>ref readonly </c> for a member returning by reference; empty otherwise.
/// </param>
internal sealed record PropertyModel(
    PropertyKind Kind,
    string Type,
    string Name,
    EquatableArray<ParameterModel> Parameters,
    bool HasGetter,
    bool HasSetter,
    bool InitOnly,
    string RefPrefix,
    string Receiver = "_inner");
