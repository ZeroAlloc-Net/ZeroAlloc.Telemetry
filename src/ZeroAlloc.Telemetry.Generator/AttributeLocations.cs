using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// Finds where an attribute argument is written, so a diagnostic about a path points at the
/// path rather than at the method.
/// </summary>
internal static class AttributeLocations
{
    /// <summary>The expression of positional argument <paramref name="index"/>, or of the argument named with <c>parameterName:</c>.</summary>
    public static Location Positional(AttributeData attribute, int index, string parameterName, Location fallback)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax syntax)
            return fallback;

        if (syntax.ArgumentList is null)
            return syntax.GetLocation();

        var position = 0;
        foreach (var argument in syntax.ArgumentList.Arguments)
        {
            // Property assignments such as When = "..." follow the constructor arguments.
            if (argument.NameEquals is not null)
                continue;

            if (argument.NameColon is not null)
            {
                if (string.Equals(argument.NameColon.Name.Identifier.ValueText, parameterName, StringComparison.Ordinal))
                    return argument.Expression.GetLocation();
            }
            else if (position == index)
            {
                return argument.Expression.GetLocation();
            }

            position++;
        }

        return syntax.GetLocation();
    }

    /// <summary>The expression assigned to <paramref name="propertyName"/>, as in <c>When = "IsSuccess"</c>.</summary>
    public static Location Named(AttributeData attribute, string propertyName, Location fallback)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax syntax)
            return fallback;

        if (syntax.ArgumentList is null)
            return syntax.GetLocation();

        foreach (var argument in syntax.ArgumentList.Arguments)
        {
            if (argument.NameEquals is { } nameEquals
                && string.Equals(nameEquals.Name.Identifier.ValueText, propertyName, StringComparison.Ordinal))
            {
                return argument.Expression.GetLocation();
            }
        }

        return syntax.GetLocation();
    }
}
