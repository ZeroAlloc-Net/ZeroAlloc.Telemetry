using System.Text;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// Resolves the dotted member paths used by <c>[TraceTag]</c>, <c>[TraceTagFromResult]</c>,
/// <c>[CountFromResult]</c>, <c>[HistogramFromResult]</c> and every <c>When</c> guard. One
/// resolver gives one set of nullable rules and one meaning of a path.
/// </summary>
/// <remarks>
/// The operator for each segment cannot be chosen from the path text: <c>?.</c> is required
/// wherever the preceding value may be null, and is a compile error wherever it cannot be. So
/// each segment is resolved to a symbol and the operator is picked from the type before it.
/// </remarks>
internal static class PathResolver
{
    /// <summary>The outcome of resolving one path.</summary>
    internal sealed class Resolution
    {
        public Resolution(string? access, ITypeSymbol? finalType, bool canBeNull, string? missingSegment, ITypeSymbol? missingOn)
        {
            Access = access;
            FinalType = finalType;
            CanBeNull = canBeNull;
            MissingSegment = missingSegment;
            MissingOn = missingOn;
        }

        /// <summary>
        /// The access to append to the root, for example <c>?.Value?.Count</c>. Empty means the
        /// root itself. Null when the path does not resolve.
        /// </summary>
        public string? Access { get; }

        /// <summary>
        /// The type the path ends at. A trailing <c>Value</c> on a nullable value type ends at
        /// the underlying type, with <see cref="CanBeNull"/> set.
        /// </summary>
        public ITypeSymbol? FinalType { get; }

        /// <summary>Whether the emitted expression can evaluate to null.</summary>
        public bool CanBeNull { get; }

        /// <summary>The first segment that names nothing. Empty for an empty segment, as in <c>A..B</c>.</summary>
        public string? MissingSegment { get; }

        /// <summary>The type <see cref="MissingSegment"/> was looked up on.</summary>
        public ITypeSymbol? MissingOn { get; }

        public bool Resolved => Access is not null;
    }

    /// <summary>
    /// Resolves <paramref name="path"/> against <paramref name="rootType"/>. An empty or
    /// whitespace path resolves to the root itself.
    /// </summary>
    /// <param name="compilation">
    /// The compilation the proxy is generated into. A member only resolves when the proxy, which
    /// lives in this assembly, can read it.
    /// </param>
    /// <param name="rootType">The type the path starts from.</param>
    /// <param name="path">The dotted member path.</param>
    public static Resolution Resolve(Compilation compilation, ITypeSymbol rootType, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new Resolution(string.Empty, rootType, CanBeNull(rootType), null, null);

        var current = rootType;
        var sb = new StringBuilder();

        // Set after a Value segment on a Nullable<T> has been dropped. The expression is still
        // nullable, but current is already T, so the next segment is looked up on T and needs ?.
        var unwrapped = false;

        foreach (var rawSegment in path.Split('.'))
        {
            var segment = rawSegment.Trim();

            // Members of dynamic bind at run time, and 1.6.4 emitted such paths unchecked. The
            // rest is appended null-safe and ends at dynamic. An empty segment is still a typo.
            if (IsDynamic(current))
            {
                if (segment.Length == 0)
                    return new Resolution(null, null, false, segment, current);

                sb.Append("?.").Append(segment);
                continue;
            }

            var nullable = unwrapped || CanBeNull(current);
            var underlying = UnwrapNullable(current);

            // `x?.Value` on a Nullable<T> does not mean Nullable<T>.Value: the null-conditional
            // unwraps first, so the member is looked up on T and `.Value` fails to compile. The
            // segment is also redundant. So drop it and carry on from T. The expression stays
            // nullable, and 1.6.4 lost exactly that: the next segment got `.` against a
            // Nullable<T>, and a guard ending here was emitted as a bare bool?.
            if (!unwrapped && IsNullableValueType(current)
                && string.Equals(segment, "Value", StringComparison.Ordinal))
            {
                current = underlying;
                unwrapped = true;
                continue;
            }

            var memberType = segment.Length == 0 ? null : FindMemberType(compilation, underlying, segment);
            if (memberType is null)
                return new Resolution(null, null, false, segment, underlying);

            sb.Append(nullable ? "?." : ".").Append(segment);
            current = memberType;
            unwrapped = false;
        }

        // May be empty when every segment resolved away, as with a bare "Value" on a nullable
        // result. That is a successful resolution meaning "the root itself".
        var access = sb.ToString();
        var canBeNull = unwrapped
            || access.IndexOf("?.", StringComparison.Ordinal) >= 0
            || CanBeNull(current);

        return new Resolution(access, current, canBeNull, null, null);
    }

    /// <summary>Whether the type is <c>dynamic</c>, whose members are only known at run time.</summary>
    public static bool IsDynamic(ITypeSymbol type) => type.TypeKind == TypeKind.Dynamic;

    /// <summary>
    /// A reference type, <c>Nullable&lt;T&gt;</c>, or a type parameter not constrained to a value
    /// type, which may be either.
    /// </summary>
    public static bool CanBeNull(ITypeSymbol type) =>
        type.IsReferenceType || IsNullableValueType(type) || type is ITypeParameterSymbol { IsValueType: false };

    /// <summary>Returns T for <c>Nullable&lt;T&gt;</c>, otherwise the type unchanged.</summary>
    public static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
        IsNullableValueType(type) && type is INamedTypeSymbol { TypeArguments.Length: 1 } n
            ? n.TypeArguments[0]
            : type;

    /// <summary><c>bool</c> or <c>bool?</c>.</summary>
    public static bool IsBoolean(ITypeSymbol type) =>
        UnwrapNullable(type).SpecialType == SpecialType.System_Boolean;

    private static bool IsNullableValueType(ITypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    /// <summary>
    /// Finds a property or field the proxy can read, walking base types and then interfaces.
    /// </summary>
    /// <remarks>
    /// A member counts only when <c>result.Member</c> would compile in the proxy: an instance
    /// field, or an instance property with a getter, that is accessible from this assembly.
    /// Inaccessible members are skipped, as C# lookup skips them. A static or set-only member is
    /// what C# would bind to and then reject, so it ends the search as not found.
    /// </remarks>
    private static ITypeSymbol? FindMemberType(Compilation compilation, ITypeSymbol type, string name)
    {
        // A type parameter has the members of its constraints, as C# lookup gives it.
        if (type is ITypeParameterSymbol parameter)
        {
            foreach (var constraint in parameter.ConstraintTypes)
            {
                if (FindMemberType(compilation, constraint, name) is { } found)
                    return found;
            }

            return null;
        }

        for (var t = type; t is not null; t = t.BaseType)
        {
            if (TryFindReadable(compilation, t, name, out var found))
                return found;
        }

        // Interfaces do not inherit through BaseType, so check the full interface set too.
        // ICollection<T>.Count on an IReadOnlyList<T> is the common case.
        foreach (var iface in type.AllInterfaces)
        {
            if (TryFindReadable(compilation, iface, name, out var found))
                return found;
        }

        return null;
    }

    /// <summary>
    /// True when <paramref name="type"/> declares an accessible member named
    /// <paramref name="name"/>, which ends the search. <paramref name="memberType"/> is its type
    /// when it can be read, and null when it cannot.
    /// </summary>
    private static bool TryFindReadable(Compilation compilation, ITypeSymbol type, string name, out ITypeSymbol? memberType)
    {
        memberType = null;
        foreach (var m in type.GetMembers(name))
        {
            if (m is not (IPropertySymbol { Parameters.Length: 0 } or IFieldSymbol))
                continue;

            if (!compilation.IsSymbolAccessibleWithin(m, compilation.Assembly))
                continue;

            if (m.IsStatic)
                return true;

            if (m is IFieldSymbol f)
            {
                memberType = f.Type;
                return true;
            }

            var p = (IPropertySymbol)m;
            if (p.GetMethod is { } getter && compilation.IsSymbolAccessibleWithin(getter, compilation.Assembly))
                memberType = p.Type;

            return true;
        }

        return false;
    }
}
