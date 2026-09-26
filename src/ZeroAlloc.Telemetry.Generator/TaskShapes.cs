using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// Decides whether a return type is awaited by the proxy, and what it yields. Recognises
/// <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c> and <c>ValueTask&lt;T&gt;</c>, and any
/// task-like type marked <c>[AsyncMethodBuilder]</c>, such as PooledAwait's <c>PooledTask&lt;T&gt;</c>.
/// </summary>
/// <remarks>
/// <para>
/// 1.6.4 searched the return type's display string for <c>"Task"</c>, so a type such as
/// <c>MyTaskResult</c> made the proxy emit <c>async</c> and <c>await</c> for a synchronous method.
/// Matching the BCL types alone went too far the other way: a custom task-like type then got a
/// synchronous proxy, whose span and histogram ended when the task was returned rather than when
/// it completed. So both are matched by symbol: the BCL types by namespace and metadata name, and
/// task-like types by the attribute that makes an <c>async</c> method able to return them.
/// </para>
/// <para>
/// A task-like type's result is the return type of <c>GetAwaiter().GetResult()</c>, which is what
/// <c>await</c> yields. A <c>void</c> there makes it awaitable with no result.
/// </para>
/// <para>
/// <c>Compilation.GetTypeByMetadataName</c> is not used: it returns null when more than one
/// referenced assembly defines the type, which a <c>ValueTask</c> or <c>AsyncMethodBuilderAttribute</c>
/// polyfill next to the BCL does. Every method would then silently be treated as synchronous.
/// </para>
/// </remarks>
internal static class TaskShapes
{
    private const string TasksNamespace = "System.Threading.Tasks";
    private const string CompilerServicesNamespace = "System.Runtime.CompilerServices";

    /// <summary>Whether the proxy has to <c>await</c> the inner call.</summary>
    public static bool IsAwaitable(ITypeSymbol type) => Classify(type).Awaitable;

    /// <summary>Awaitable with no result: <c>Task</c>, <c>ValueTask</c>, or a task-like whose <c>GetResult</c> is void.</summary>
    public static bool IsVoidAwaitable(ITypeSymbol type)
    {
        var shape = Classify(type);
        return shape.Awaitable && shape.Result is null;
    }

    /// <summary>The awaited type for an awaitable with a result, otherwise the type itself.</summary>
    public static ITypeSymbol UnwrapAwaited(ITypeSymbol type) =>
        Classify(type).Result ?? type;

    /// <summary>
    /// Whether <paramref name="type"/> is awaited, and the type <c>await</c> yields. The result is
    /// null for a void awaitable and for a type that is not awaited.
    /// </summary>
    private static (bool Awaitable, ITypeSymbol? Result) Classify(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol named)
            return (false, null);

        if (IsBcl(named, "Task") || IsBcl(named, "ValueTask"))
            return (true, null);

        if ((IsBcl(named, "Task`1") || IsBcl(named, "ValueTask`1")) && named.TypeArguments.Length == 1)
            return (true, named.TypeArguments[0]);

        if (HasAsyncMethodBuilder(named) && GetResultMethod(named) is { } getResult)
            return (true, getResult.ReturnsVoid ? null : getResult.ReturnType);

        return (false, null);
    }

    private static bool IsBcl(INamedTypeSymbol named, string metadataName) =>
        IsNamed(named.OriginalDefinition, TasksNamespace, metadataName);

    /// <summary>
    /// Whether the type's definition carries <c>AsyncMethodBuilderAttribute</c>, which is what lets
    /// the proxy be an <c>async</c> method returning it.
    /// </summary>
    private static bool HasAsyncMethodBuilder(INamedTypeSymbol named)
    {
        foreach (var attr in named.OriginalDefinition.GetAttributes())
        {
            if (attr.AttributeClass is { } cls
                && IsNamed(cls, CompilerServicesNamespace, "AsyncMethodBuilderAttribute"))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// <c>GetResult()</c> on the awaiter returned by the instance, parameterless
    /// <c>GetAwaiter()</c>. Looked up on the constructed type, so a generic task-like yields its
    /// type argument.
    /// </summary>
    private static IMethodSymbol? GetResultMethod(INamedTypeSymbol type)
    {
        var getAwaiter = FindParameterlessInstanceMethod(type, "GetAwaiter");
        if (getAwaiter is null || getAwaiter.ReturnsVoid)
            return null;

        return FindParameterlessInstanceMethod(getAwaiter.ReturnType, "GetResult");
    }

    private static IMethodSymbol? FindParameterlessInstanceMethod(ITypeSymbol type, string name)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            foreach (var member in t.GetMembers(name))
            {
                if (member is IMethodSymbol { IsStatic: false, Parameters.Length: 0, TypeParameters.Length: 0 } method)
                    return method;
            }
        }

        return null;
    }

    private static bool IsNamed(INamedTypeSymbol type, string ns, string metadataName) =>
        string.Equals(type.MetadataName, metadataName, StringComparison.Ordinal)
        && type.ContainingNamespace is { } containing
        && string.Equals(containing.ToDisplayString(), ns, StringComparison.Ordinal);
}
