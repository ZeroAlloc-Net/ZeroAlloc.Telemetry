using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using ZeroAlloc.Telemetry.Generator.Models;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// The declarations a proxy is emitted inside: the partial declarations of the interface's
/// containing types, outermost first. A port of ZeroAlloc.Mapping's <c>HostDeclarations</c>, plus
/// the constraint clauses a generic proxy repeats from its interface.
/// </summary>
internal static class TypeDeclarations
{
    /// <summary>
    /// The outermost containing type of <paramref name="type"/> that is not declared
    /// <c>partial</c>, or null when all of them are. The generated code has to reopen every
    /// containing type, which only a partial type allows.
    /// </summary>
    public static INamedTypeSymbol? FirstNonPartialContainingType(INamedTypeSymbol type)
    {
        INamedTypeSymbol? outermost = null;
        for (var t = type.ContainingType; t is not null; t = t.ContainingType)
        {
            if (!IsPartial(t)) outermost = t;
        }
        return outermost;
    }

    /// <summary>
    /// The file-local type that <paramref name="type"/> is or is nested in, or null. A
    /// file-local type is visible only in its own file, so a generated file cannot use it.
    /// </summary>
    public static INamedTypeSymbol? FileLocalType(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.ContainingType)
        {
            if (t.IsFileLocal) return t;
        }
        return null;
    }

    /// <summary>
    /// The innermost containing interface of <paramref name="type"/> with an <c>in</c> or
    /// <c>out</c> type parameter, or null. C# does not allow a class inside such an interface.
    /// </summary>
    public static INamedTypeSymbol? VariantContainingInterface(INamedTypeSymbol type)
    {
        for (var t = type.ContainingType; t is not null; t = t.ContainingType)
        {
            if (t.TypeKind == TypeKind.Interface && t.TypeParameters.Any(static p => p.Variance != VarianceKind.None))
                return t;
        }
        return null;
    }

    /// <summary>
    /// The partial declaration of every containing type, outermost first, such as
    /// <c>partial struct Holder&lt;T&gt;</c>. Each carries its kind and type parameter names, and
    /// keyword names are written as verbatim identifiers. Empty for a top-level type.
    /// </summary>
    public static EquatableArray<string> ContainingDeclarations(INamedTypeSymbol type)
    {
        var chain = new List<string>();
        for (var t = type.ContainingType; t is not null; t = t.ContainingType)
        {
            var sb = new StringBuilder();
            if (t.IsRefLikeType) sb.Append("ref ");
            sb.Append("partial ").Append(Keyword(t)).Append(' ').Append(Identifier(t.Name));
            sb.Append(TypeParameterList(t));
            chain.Add(sb.ToString());
        }
        chain.Reverse();
        return new EquatableArray<string>(chain.ToImmutableArray());
    }

    /// <summary>
    /// The type parameter names of <paramref name="type"/>, as in <c>&lt;T, U&gt;</c>, or empty
    /// when it has none. Variance is left out: a class cannot declare it, and a containing
    /// interface with a variant type parameter cannot hold the proxy at all, ZTEL017.
    /// </summary>
    public static string TypeParameterList(INamedTypeSymbol type)
    {
        if (type.TypeParameters.Length == 0) return string.Empty;
        return "<" + string.Join(", ", type.TypeParameters.Select(static p => Identifier(p.Name))) + ">";
    }

    /// <summary>
    /// A <c>where</c> clause for each type parameter of <paramref name="type"/> that has
    /// constraints, in declaration order. A proxy is a new type, so it has to repeat them to
    /// implement the interface.
    /// </summary>
    public static EquatableArray<string> ConstraintClauses(INamedTypeSymbol type, SymbolDisplayFormat typeFormat)
    {
        var clauses = ImmutableArray.CreateBuilder<string>();
        foreach (var parameter in type.TypeParameters)
        {
            var constraints = new List<string>();
            if (parameter.HasReferenceTypeConstraint)
            {
                constraints.Add(parameter.ReferenceTypeConstraintNullableAnnotation == NullableAnnotation.Annotated
                    ? "class?"
                    : "class");
            }
            else if (parameter.HasUnmanagedTypeConstraint)
            {
                constraints.Add("unmanaged");
            }
            else if (parameter.HasValueTypeConstraint)
            {
                constraints.Add("struct");
            }
            else if (parameter.HasNotNullConstraint)
            {
                constraints.Add("notnull");
            }

            foreach (var constraintType in parameter.ConstraintTypes)
                constraints.Add(constraintType.ToDisplayString(typeFormat));

            if (parameter.HasConstructorConstraint) constraints.Add("new()");
            if (parameter.AllowsRefLikeType) constraints.Add("allows ref struct");

            if (constraints.Count > 0)
                clauses.Add("where " + Identifier(parameter.Name) + " : " + string.Join(", ", constraints));
        }
        return new EquatableArray<string>(clauses.ToImmutable());
    }

    /// <summary>A name written as an identifier: a keyword gets the verbatim <c>@</c> prefix.</summary>
    public static string Identifier(string name) =>
        SyntaxFacts.GetKeywordKind(name) != SyntaxKind.None ? "@" + name : name;

    private static bool IsPartial(INamedTypeSymbol type) =>
        type.DeclaringSyntaxReferences.Any(static r =>
            r.GetSyntax() is TypeDeclarationSyntax declaration &&
            declaration.Modifiers.Any(static m => m.IsKind(SyntaxKind.PartialKeyword)));

    private static string Keyword(INamedTypeSymbol type) => type switch
    {
        { IsRecord: true, TypeKind: TypeKind.Struct } => "record struct",
        { IsRecord: true } => "record",
        { TypeKind: TypeKind.Struct } => "struct",
        { TypeKind: TypeKind.Interface } => "interface",
        _ => "class",
    };
}
