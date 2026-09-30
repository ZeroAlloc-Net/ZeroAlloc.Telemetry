using System.Globalization;
using System.Text;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// Hint names of generated files.
/// </summary>
internal static class HintNames
{
    /// <summary>
    /// The hint name of the proxy file for an instrumented interface, unique within the
    /// compilation: the namespace, then the containing types and the interface joined by
    /// <c>+</c>, each with its arity, then <c>.Instrumented.g.cs</c>, as in
    /// <c>App.Outer`1+IOrders.Instrumented.g.cs</c>. An interface in the global namespace has no
    /// namespace part.
    /// </summary>
    /// <remarks>
    /// The name is the interface's own, not the proxy's, which drops a leading <c>I</c>: the
    /// interfaces <c>IOrders</c> and <c>Orders</c> would otherwise share a file name. Nesting is
    /// written with <c>+</c> rather than a dot, so a type nested in <c>App.Outer</c> and a type at
    /// the top of namespace <c>App.Outer</c> never share a name. Roslyn compares hint names
    /// ignoring case, so interfaces whose names differ only in case would collide. The later one
    /// gets no proxy and ZTEL015; see <see cref="ProxyCollisions"/>.
    /// </remarks>
    public static string ForInterface(INamedTypeSymbol type)
    {
        var sb = new StringBuilder();
        AppendNamespace(sb, type.ContainingNamespace);
        AppendTypeChain(sb, type);
        return Sanitize(sb.ToString()) + ".Instrumented.g.cs";
    }

    /// <summary>
    /// Keeps the characters an identifier, a namespace separator or an arity is written with,
    /// and escapes every other UTF-16 code unit as <c>-uXXXX</c>. No identifier contains a
    /// <c>-</c>, so an escaped name never collides with a name that needed no escaping.
    /// </summary>
    public static string Sanitize(string name)
    {
        var sb = new StringBuilder(name.Length);
        for (var i = 0; i < name.Length; i++)
        {
            var c = name[i];
            if (c is '.' or '+' or '`')
            {
                sb.Append(c);
                continue;
            }

            if (char.IsHighSurrogate(c) && i + 1 < name.Length && char.IsLowSurrogate(name[i + 1]) &&
                IsIdentifierCategory(CharUnicodeInfo.GetUnicodeCategory(name, i)))
            {
                sb.Append(c).Append(name[i + 1]);
                i++;
                continue;
            }

            if (!char.IsSurrogate(c) && IsIdentifierCategory(CharUnicodeInfo.GetUnicodeCategory(c)))
            {
                sb.Append(c);
                continue;
            }

            sb.Append("-u").Append(((int)c).ToString("X4", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private static void AppendNamespace(StringBuilder sb, INamespaceSymbol? ns)
    {
        if (ns is null || ns.IsGlobalNamespace) return;
        AppendNamespace(sb, ns.ContainingNamespace);
        sb.Append(ns.Name).Append('.');
    }

    private static void AppendTypeChain(StringBuilder sb, INamedTypeSymbol type)
    {
        if (type.ContainingType is { } outer)
        {
            AppendTypeChain(sb, outer);
            sb.Append('+');
        }
        sb.Append(type.Name);
        if (type.Arity > 0) sb.Append('`').Append(type.Arity.ToString(CultureInfo.InvariantCulture));
    }

    private static bool IsIdentifierCategory(UnicodeCategory category) => category is
        UnicodeCategory.UppercaseLetter or UnicodeCategory.LowercaseLetter or
        UnicodeCategory.TitlecaseLetter or UnicodeCategory.ModifierLetter or
        UnicodeCategory.OtherLetter or UnicodeCategory.LetterNumber or
        UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or
        UnicodeCategory.DecimalDigitNumber or UnicodeCategory.ConnectorPunctuation;
}
