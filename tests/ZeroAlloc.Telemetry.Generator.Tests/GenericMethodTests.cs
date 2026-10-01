using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// A generic method on an instrumented interface gets a proxy method with the same type parameters
/// and constraint clauses (#168). Before the fix the proxy method was written without either, so it
/// did not implement the interface: CS0535, and CS0246 for every use of the type parameter.
/// </summary>
public class GenericMethodTests
{
    [Fact]
    public void GenericMethods_WithEveryConstraintKind_Compile()
    {
        var run = GeneratorCompilation.RunAll("""
            using System;
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            namespace App
            {
                public sealed class Entity { }
                [Instrument("a")]
                public interface IOps
                {
                    [Trace("plain")] T Echo<T>(T value);
                    [Trace("class")] Task<T?> FindAsync<T>(string id) where T : class;
                    [Trace("nullable-class")] T Nullable<T>(T value) where T : class?;
                    [Trace("struct")] ValueTask<T> ParseAsync<T>(string text) where T : struct;
                    [Count("unmanaged")] int Size<T>() where T : unmanaged;
                    [Histogram("notnull")] void Store<TKey, TValue>(TKey key, TValue value) where TKey : notnull;
                    [Trace("new")] T Create<T>() where T : new();
                    [Trace("type")] TSet Copy<TSet>(TSet set) where TSet : ISet<TSet>;
                    [Trace("combined")] T Combined<T, TBase>(T value) where TBase : class where T : TBase, IDisposable, new();
                    [Trace("ref-struct")] int Length<T>(T value) where T : allows ref struct;
                    [Trace("ref-struct-constrained")] int Count<T>(T value) where T : struct, allows ref struct;
                    void Untraced<T>(T value) where T : IComparable<T>;
                    [Trace("enum")] T Flag<T>(T value) where T : struct, Enum;
                    [Trace("delegate")] T Wrap<T>(T value) where T : Delegate;
                }
                internal sealed class Ops : IOps
                {
                    public T Echo<T>(T value) => value;
                    public Task<T?> FindAsync<T>(string id) where T : class => Task.FromResult<T?>(null);
                    public T Nullable<T>(T value) where T : class? => value;
                    public ValueTask<T> ParseAsync<T>(string text) where T : struct => default;
                    public int Size<T>() where T : unmanaged => 0;
                    public void Store<TKey, TValue>(TKey key, TValue value) where TKey : notnull { }
                    public T Create<T>() where T : new() => new T();
                    public TSet Copy<TSet>(TSet set) where TSet : ISet<TSet> => set;
                    public T Combined<T, TBase>(T value) where TBase : class where T : TBase, IDisposable, new() => value;
                    public int Length<T>(T value) where T : allows ref struct => 0;
                    public int Count<T>(T value) where T : struct, allows ref struct => 0;
                    public void Untraced<T>(T value) where T : IComparable<T> { }
                    public T Flag<T>(T value) where T : struct, Enum => value;
                    public T Wrap<T>(T value) where T : Delegate => value;
                }
                internal static class Calls
                {
                    public static int Use()
                    {
                        IOps ops = new OpsInstrumented(new Ops());
                        ops.Store("k", 1);
                        ops.Untraced(1);
                        return ops.Echo(1) + ops.Size<int>() + ops.Length("x".AsSpan()) + ops.Count("x".AsSpan());
                    }
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
    }

    [Fact]
    public void GenericMethod_OnAGenericInterface_Compiles()
    {
        var run = GeneratorCompilation.RunAll("""
            using System;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            namespace App
            {
                [Instrument("a")]
                public interface IConverter<TSource> where TSource : notnull
                {
                    [Trace("convert")] ValueTask<TTarget> ConvertAsync<TTarget>(TSource source) where TTarget : TSource;
                    [Trace("pair")] (TSource, T) Pair<T>(TSource source, T other) where T : IEquatable<TSource>;
                }
                internal sealed class Converter<TSource> : IConverter<TSource> where TSource : notnull
                {
                    public ValueTask<TTarget> ConvertAsync<TTarget>(TSource source) where TTarget : TSource => new((TTarget)source);
                    public (TSource, T) Pair<T>(TSource source, T other) where T : IEquatable<TSource> => (source, other);
                }
                internal static class Calls
                {
                    public static ValueTask<string> Use() =>
                        new ConverterInstrumented<object>(new Converter<object>()).ConvertAsync<string>("x");
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
    }

    /// <summary>
    /// A type parameter that appears only in the return type cannot be inferred, so the forwarded
    /// call names the type arguments.
    /// </summary>
    [Fact]
    public void GenericMethod_WhoseTypeParameterIsOnlyInTheReturnType_ForwardsTheTypeArgument()
    {
        var run = GeneratorCompilation.RunAll("""
            using ZeroAlloc.Telemetry;
            namespace App
            {
                [Instrument("a")]
                public interface IFactory
                {
                    T Make<T>() where T : new();
                    [Trace("make")] T MakeTraced<T>() where T : new();
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        var proxy = run.Output.SyntaxTrees.First(t => t.FilePath.EndsWith("IFactory.Instrumented.g.cs", StringComparison.Ordinal)).ToString();
        proxy.Should().Contain("_inner.Make<T>()");
        proxy.Should().Contain("_inner.MakeTraced<T>()");
    }

    /// <summary>
    /// The ZeroAlloc.Jev shape: an internal interface whose generic methods return a
    /// <c>ValueTask</c> of a result struct, with a self-referencing set constraint.
    /// </summary>
    [Fact]
    public void JevOperationsShape_Compiles()
    {
        var run = GeneratorCompilation.RunAll("""
            using System.Collections.Generic;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            namespace Jev
            {
                public readonly struct Result<T, E>
                {
                    public Result(T value) { Value = value; Error = default; IsFailure = false; }
                    public T Value { get; }
                    public E? Error { get; }
                    public bool IsFailure { get; }
                }
                public sealed class JevError { }
                [Instrument("ZeroAlloc.Jev")]
                internal interface IJevOperations
                {
                    [Trace("jev.union")]
                    [Count("jev.operations")]
                    [Histogram("jev.duration", Unit = "ms")]
                    ValueTask<Result<T, JevError>> UnionAsync<T>(T left, T right) where T : ISet<T>;

                    [Trace("jev.evaluate")]
                    ValueTask<Result<T, E>> EvaluateAsync<T, E>(T input) where T : ISet<T> where E : notnull;
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
    }

    /// <summary>
    /// Keyword names are written as verbatim identifiers in the proxy method: its name, its type
    /// parameters and its parameters, and the forwarded call.
    /// </summary>
    [Fact]
    public void KeywordNamedGenericMethod_TypeParameterAndParameter_Compile()
    {
        var run = GeneratorCompilation.RunAll("""
            using ZeroAlloc.Telemetry;
            namespace App
            {
                [Instrument("a")]
                public interface IKeywords
                {
                    [Trace("event")] @int @event<@int>(@int @class) where @int : struct;
                    void @void<@object>(@object @string);
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
    }
}
