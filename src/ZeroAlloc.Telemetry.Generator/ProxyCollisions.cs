using System.Collections.Generic;
using System.Collections.Immutable;
using ZeroAlloc.Telemetry.Generator.Models;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// The proxies that cannot be added because an earlier proxy already has their file name,
/// ignoring case as Roslyn compares hint names (ZTEL015), or their name, as C# compares it
/// (ZTEL016), and the errors about them. The scheme is the one ZeroAlloc.Mapping uses.
/// </summary>
internal sealed record ProxyCollisions(
    EquatableArray<string> SkippedHintNames,
    EquatableArray<DiagnosticInfo> Diagnostics)
{
    /// <summary>
    /// Goes through the proxies in declaration order, by file path and then position. The first
    /// proxy with a file name or a proxy name keeps it; every later one is skipped and gets an
    /// error on its interface's name. The order does not depend on the order the pipeline
    /// delivers the interfaces in, so the same proxy is generated on every run.
    /// </summary>
    public static ProxyCollisions Find(ImmutableArray<ProxyFile> files)
    {
        var sorted = files.ToArray();
        System.Array.Sort(sorted, CompareDeclarationOrder);

        var byHintName = new Dictionary<string, ProxyFile>(System.StringComparer.OrdinalIgnoreCase);
        var byProxyKey = new Dictionary<string, ProxyFile>(System.StringComparer.Ordinal);
        var skipped = ImmutableArray.CreateBuilder<string>();
        var diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
        foreach (var file in sorted)
        {
            if (byHintName.TryGetValue(file.HintName, out var sameFile))
            {
                skipped.Add(file.HintName);
                diagnostics.Add(Create(
                    InstrumentDiagnostics.NameDiffersOnlyInCase,
                    file, file.HintName, sameFile.InterfaceDisplayName));
                continue;
            }

            if (byProxyKey.TryGetValue(file.ProxyKey, out var sameProxy))
            {
                skipped.Add(file.HintName);
                diagnostics.Add(Create(
                    InstrumentDiagnostics.ProxyNameCollision,
                    file, file.ProxyDisplayName, sameProxy.InterfaceDisplayName));
                continue;
            }

            byHintName.Add(file.HintName, file);
            byProxyKey.Add(file.ProxyKey, file);
        }

        return new ProxyCollisions(
            new EquatableArray<string>(skipped.ToImmutable()),
            new EquatableArray<DiagnosticInfo>(diagnostics.ToImmutable()));
    }

    /// <summary>Whether the file with <paramref name="hintName"/> must not be added.</summary>
    public bool Skips(string hintName)
    {
        foreach (var skippedName in SkippedHintNames)
        {
            if (string.Equals(skippedName, hintName, System.StringComparison.Ordinal)) return true;
        }
        return false;
    }

    private static DiagnosticInfo Create(
        Microsoft.CodeAnalysis.DiagnosticDescriptor descriptor, ProxyFile file, string name, string earlier) =>
        new(descriptor, file.Location, new EquatableArray<string>(
            ImmutableArray.Create(file.InterfaceDisplayName, name, earlier)));

    private static int CompareDeclarationOrder(ProxyFile x, ProxyFile y)
    {
        var byPath = string.CompareOrdinal(x.Location.Tree.FilePath, y.Location.Tree.FilePath);
        if (byPath != 0) return byPath;
        var byPosition = x.Location.Span.Start.CompareTo(y.Location.Span.Start);
        return byPosition != 0 ? byPosition : string.CompareOrdinal(x.HintName, y.HintName);
    }
}
