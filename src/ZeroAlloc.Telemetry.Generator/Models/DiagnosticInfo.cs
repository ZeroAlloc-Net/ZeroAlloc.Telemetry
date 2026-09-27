using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>
/// A diagnostic found while building the model, reported when the proxy is emitted.
/// </summary>
/// <remarks>
/// A <see cref="Diagnostic"/> compares its message arguments by reference, so one rebuilt from
/// the same source never compares equal to the last, and a model holding it is never served from
/// the cache. Here the arguments are strings compared by value, and the location is a
/// <see cref="LocationInfo"/>. The descriptor itself is kept rather than its id: two ZTEL007
/// descriptors share an id and differ in message.
/// </remarks>
internal sealed record DiagnosticInfo(
    DiagnosticDescriptor Descriptor,
    LocationInfo? Location,
    EquatableArray<string> MessageArgs)
{
    /// <summary>
    /// Message arguments are strings, so they compare by value. A null argument formats as
    /// empty, the same as <see cref="Diagnostic.Create(DiagnosticDescriptor, Microsoft.CodeAnalysis.Location, object[])"/>
    /// formats it.
    /// </summary>
    public static DiagnosticInfo Create(DiagnosticDescriptor descriptor, Location? location, params string?[] messageArgs)
    {
        var args = ImmutableArray.CreateBuilder<string>(messageArgs.Length);
        foreach (var arg in messageArgs)
            args.Add(arg ?? string.Empty);

        return new DiagnosticInfo(descriptor, LocationInfo.From(location), new EquatableArray<string>(args.MoveToImmutable()));
    }

    public Diagnostic ToDiagnostic()
    {
        var args = new object[MessageArgs.Count];
        for (var i = 0; i < args.Length; i++)
            args[i] = MessageArgs[i];

        return Diagnostic.Create(Descriptor, Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None, args);
    }
}
