using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// A nested instrumented interface gets its proxy next to it, inside partial declarations of its
/// containing types, and a generic one gets a generic proxy with the interface's constraints
/// (#162). An interface whose proxy cannot be generated is reported on the interface, and every
/// other proxy is still generated.
/// </summary>
public class NestedAndGenericInterfaceTests
{
    [Fact]
    public void NestedInterfaces_WithTheSameName_GetProxiesInTheirContainingTypes()
    {
        var run = GeneratorCompilation.RunAll("""
            using ZeroAlloc.Telemetry;
            namespace App
            {
                public partial class O1
                {
                    [Instrument("a")] public interface IFoo { [Trace("o1")] int Run(int x); }
                }
                public partial class O2<T>
                {
                    [Instrument("b")] public interface IFoo { [Trace("o2")] T Run(T x); }
                }
                public partial struct O3
                {
                    public partial interface IMiddle
                    {
                        [Instrument("c")] internal interface IFoo { void Run(); }
                    }
                }
                internal sealed class Impl1 : O1.IFoo { public int Run(int x) => x; }
                internal sealed class Impl2 : O2<string>.IFoo { public string Run(string x) => x; }
                internal sealed class Impl3 : O3.IMiddle.IFoo { public void Run() { } }
                internal static class Calls
                {
                    public static int Use()
                    {
                        O1.IFoo a = new O1.FooInstrumented(new Impl1());
                        O2<string>.IFoo b = new O2<string>.FooInstrumented(new Impl2());
                        O3.IMiddle.IFoo c = new O3.IMiddle.FooInstrumented(new Impl3());
                        c.Run();
                        return a.Run(1) + b.Run("x").Length;
                    }
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        run.Output.GetTypeByMetadataName("App.FooInstrumented").Should().BeNull();
        run.Output.GetTypeByMetadataName("App.O1+FooInstrumented").Should().NotBeNull();
        run.Output.GetTypeByMetadataName("App.O2`1+FooInstrumented").Should().NotBeNull();
        run.Output.GetTypeByMetadataName("App.O3+IMiddle+FooInstrumented").Should().NotBeNull();
    }

    [Fact]
    public void GenericInterfaces_GetGenericProxiesWithTheirConstraints()
    {
        var run = GeneratorCompilation.RunAll("""
            using System;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            namespace App
            {
                public sealed class Entity { }
                [Instrument("a")] public interface IFoo { void A(); }
                [Instrument("b")] public interface IFoo<T> { [Trace("b")] void A(T t); }
                [Instrument("c")]
                public interface IRepo<TKey, TValue> where TKey : struct, IComparable<TKey> where TValue : class?, new()
                {
                    [Trace("get")] TValue? Get(TKey key);
                    Task<TValue> GetAsync(TKey key);
                }
                [Instrument("d")] public interface IVariant<in TIn, out TOut> { TOut Map(TIn x); }
                [Instrument("e")]
                public interface IAll<T1, T2, T3, T4, T5>
                    where T1 : unmanaged
                    where T2 : notnull
                    where T3 : class
                    where T4 : struct
                    where T5 : T3
                {
                    void Run(T1 a, T2 b, T3 c, T4 d, T5 e);
                }
                internal sealed class Foo : IFoo { public void A() { } }
                internal sealed class Foo<T> : IFoo<T> { public void A(T t) { } }
                internal sealed class Repo : IRepo<int, Entity>
                {
                    public Entity? Get(int key) => null;
                    public Task<Entity> GetAsync(int key) => Task.FromResult(new Entity());
                }
                internal sealed class Variant : IVariant<object, string> { public string Map(object x) => ""; }
                internal static class Calls
                {
                    public static void Use()
                    {
                        IFoo a = new FooInstrumented(new Foo());
                        IFoo<int> b = new FooInstrumented<int>(new Foo<int>());
                        IRepo<int, Entity> c = new RepoInstrumented<int, Entity>(new Repo());
                        IVariant<object, string> d = new VariantInstrumented<object, string>(new Variant());
                        a.A(); b.A(1); c.Get(1); d.Map(1);
                    }
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        run.HintNames.Should().Equal(
            "App.IAll`5.Instrumented.g.cs",
            "App.IFoo.Instrumented.g.cs",
            "App.IFoo`1.Instrumented.g.cs",
            "App.IRepo`2.Instrumented.g.cs",
            "App.IVariant`2.Instrumented.g.cs");
        run.Output.GetTypeByMetadataName("App.AllInstrumented`5").Should().NotBeNull();
    }

    [Fact]
    public void GenericInterfaceThatAllowsRefStructs_GetsAProxyThatAllowsThemToo()
    {
        var run = GeneratorCompilation.RunAll("""
            using System;
            using ZeroAlloc.Telemetry;
            namespace App
            {
                [Instrument("a")] public interface ISink<T> where T : allows ref struct { void Write(T value); }
                internal sealed class Sink : ISink<ReadOnlySpan<char>> { public void Write(ReadOnlySpan<char> value) { } }
                internal static class Calls
                {
                    public static void Use()
                    {
                        ISink<ReadOnlySpan<char>> sink = new SinkInstrumented<ReadOnlySpan<char>>(new Sink());
                        sink.Write("x".AsSpan());
                    }
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void InterfaceInAVariantInterface_ReportsZtel017_AndGetsNoProxy()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            namespace App
            {
                public partial interface IOuter<out T>
                {
                    [Instrument("a")] public interface IFoo { void Run(); }
                }
            }
            """;

        var run = GeneratorCompilation.RunAll(source);

        var ztel017 = run.GeneratorDiagnostics.Should().ContainSingle().Subject;
        ztel017.Id.Should().Be("ZTEL017");
        ztel017.Severity.Should().Be(DiagnosticSeverity.Error);
        ztel017.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "Instrumented interface 'App.IOuter<T>.IFoo' gets no proxy because its containing interface 'App.IOuter<T>' has a variant type parameter, and a class cannot be declared in it");
        Text(source, ztel017).Should().Be("IFoo");
        run.HintNames.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void VerbatimNamedInterfaceAndContainers_GetProxies()
    {
        var run = GeneratorCompilation.RunAll("""
            using ZeroAlloc.Telemetry;
            namespace App
            {
                public partial class @class<@int>
                {
                    [Instrument("a")] public interface @event { @int Run(@int x); }
                }
                [Instrument("b")] public interface @IStatic<@void> { void Run(@void v); }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Output.GetTypeByMetadataName("App.class`1+eventInstrumented").Should().NotBeNull();
        run.Output.GetTypeByMetadataName("App.StaticInstrumented`1").Should().NotBeNull();
    }

    [Fact]
    public void ContainingTypeDeclaredInSeveralParts_IsPartial()
    {
        var run = GeneratorCompilation.RunAll(
            "namespace App { public partial class Outer { } }",
            """
            using ZeroAlloc.Telemetry;
            namespace App
            {
                public partial class Outer
                {
                    [Instrument("a")] public interface IFoo { void Run(); }
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Output.GetTypeByMetadataName("App.Outer+FooInstrumented").Should().NotBeNull();
    }

    [Fact]
    public void NestedGenericInterface_EmitsThePartialDeclarationsOfItsContainingTypes()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            namespace App;
            public partial struct Holder<T>
            {
                internal static partial class Api<U>
                {
                    [Instrument("x", PublicProxy = true)]
                    public interface IFoo<V> where V : class
                    {
                        [Trace("get")] V? Get(T t, U u);
                    }
                }
            }
            """;

        GeneratorSnapshot.Verify(RunDriver(source));
    }

    [Fact]
    public void InterfaceInANonPartialContainingType_ReportsZtel013_AndGetsNoProxy()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            namespace App
            {
                public class Outer
                {
                    [Instrument("a")] public interface IFoo { void Run(); }
                }
                [Instrument("b")] public interface IKept { void Run(); }
            }
            """;

        var run = GeneratorCompilation.RunAll(source);

        var ztel013 = run.GeneratorDiagnostics.Should().ContainSingle().Subject;
        ztel013.Id.Should().Be("ZTEL013");
        ztel013.Severity.Should().Be(DiagnosticSeverity.Warning);
        ztel013.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "Instrumented interface 'App.Outer.IFoo' gets no proxy because its containing type 'App.Outer' is not partial");
        Text(source, ztel013).Should().Be("IFoo");
        run.HintNames.Should().Equal("App.IKept.Instrumented.g.cs");
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void InterfaceWhoseOuterContainingTypeIsNotPartial_ReportsZtel013_NamingThatType()
    {
        var run = GeneratorCompilation.RunAll("""
            using ZeroAlloc.Telemetry;
            namespace App
            {
                public static class Outer
                {
                    public partial class Middle
                    {
                        [Instrument("a")] public interface IFoo { void Run(); }
                    }
                }
            }
            """);

        var ztel013 = run.GeneratorDiagnostics.Should().ContainSingle().Subject;
        ztel013.Id.Should().Be("ZTEL013");
        ztel013.GetMessage(CultureInfo.InvariantCulture).Should().Contain("containing type 'App.Outer' is not partial");
        run.HintNames.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void FileLocalInterface_ReportsZtel014_AndGetsNoProxy()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            namespace App;
            [Instrument("a")] file interface IFoo { void Run(); }
            file partial class Outer
            {
                [Instrument("b")] public interface IBar { void Run(); }
            }
            [Instrument("c")] public interface IKept { void Run(); }
            """;

        var run = GeneratorCompilation.RunAll(source);

        run.GeneratorDiagnostics.Should().HaveCount(2).And.OnlyContain(d =>
            d.Id == "ZTEL014" && d.Severity == DiagnosticSeverity.Error);
        run.GeneratorDiagnostics.Should().ContainSingle(d => Text(source, d) == "IFoo").Subject
            .GetMessage(CultureInfo.InvariantCulture).Should().Be("Instrumented interface 'App.IFoo' gets no proxy because 'App.IFoo' is file-local");
        run.GeneratorDiagnostics.Should().ContainSingle(d => Text(source, d) == "IBar").Subject
            .GetMessage(CultureInfo.InvariantCulture).Should().Be("Instrumented interface 'App.Outer.IBar' gets no proxy because 'App.Outer' is file-local");
        run.HintNames.Should().Equal("App.IKept.Instrumented.g.cs");
    }

    [Fact]
    public void InterfacesDifferingOnlyInCase_ReportZtel015_OnTheLaterOne_AndGenerateTheRest()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            namespace App
            {
                [Instrument("a")] public interface IFoo { void A(); }
                [Instrument("b")] public interface Ifoo { void B(); }
                [Instrument("c")] public interface IKept { void C(); }
            }
            """;

        var run = GeneratorCompilation.RunAll(source);

        var ztel015 = run.GeneratorDiagnostics.Should().ContainSingle().Subject;
        ztel015.Id.Should().Be("ZTEL015");
        ztel015.Severity.Should().Be(DiagnosticSeverity.Error);
        ztel015.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "Instrumented interface 'App.Ifoo' gets no proxy because its file name 'App.Ifoo.Instrumented.g.cs' differs only in case from that of 'App.IFoo'");
        Text(source, ztel015).Should().Be("Ifoo");
        run.HintNames.Should().Equal("App.IFoo.Instrumented.g.cs", "App.IKept.Instrumented.g.cs");
        run.GeneratorDiagnostics.Should().NotContain(d => d.Id == "CS8785");
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void InterfacesSharingAProxyName_ReportZtel016_OnTheLaterOne_AndGenerateTheRest()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            namespace App
            {
                [Instrument("a")] public interface IFoo { void A(); }
                [Instrument("b")] public interface Foo { void B(); }
                [Instrument("c")] public interface IFoo<T> { void C(); }
                [Instrument("d")] public interface Foo<T> { void D(); }
                public partial class Outer
                {
                    [Instrument("e")] public interface IFoo { void E(); }
                    [Instrument("f")] public interface Foo { void F(); }
                }
            }
            """;

        var run = GeneratorCompilation.RunAll(source);

        run.GeneratorDiagnostics.Should().HaveCount(3).And.OnlyContain(d =>
            d.Id == "ZTEL016" && d.Severity == DiagnosticSeverity.Error && Text(source, d) == "Foo");
        run.GeneratorDiagnostics.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(
            "Instrumented interface 'App.Foo' gets no proxy because its proxy 'App.FooInstrumented' has the same name as the proxy of 'App.IFoo'",
            "Instrumented interface 'App.Foo<T>' gets no proxy because its proxy 'App.FooInstrumented<T>' has the same name as the proxy of 'App.IFoo<T>'",
            "Instrumented interface 'App.Outer.Foo' gets no proxy because its proxy 'App.Outer.FooInstrumented' has the same name as the proxy of 'App.Outer.IFoo'");
        run.HintNames.Should().Equal(
            "App.IFoo.Instrumented.g.cs",
            "App.IFoo`1.Instrumented.g.cs",
            "App.Outer+IFoo.Instrumented.g.cs");
        run.Errors.Should().BeEmpty();
    }

    /// <summary>
    /// The earlier interface is the one declared first, by file path and then position, so which
    /// proxy is generated does not depend on the order of the syntax trees in the compilation.
    /// </summary>
    [Fact]
    public void EarlierInterface_IsTheOneDeclaredFirst_AcrossFiles()
    {
        var first = CSharpSyntaxTree.ParseText(
            "using ZeroAlloc.Telemetry; namespace App; [Instrument(\"a\")] public interface Foo { void Run(); }",
            path: "a.cs");
        var second = CSharpSyntaxTree.ParseText(
            "using ZeroAlloc.Telemetry; namespace App; [Instrument(\"b\")] public interface IFoo { void Run(); }",
            path: "b.cs");
        var compilation = CSharpCompilation.Create("Probe", [second, first], References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = CSharpGeneratorDriver.Create(new InstrumentGenerator())
            .RunGenerators(compilation)
            .GetRunResult()
            .Results[0];

        result.GeneratedSources.Should().ContainSingle().Which.HintName.Should().Be("App.Foo.Instrumented.g.cs");
        var ztel016 = result.Diagnostics.Should().ContainSingle(d => d.Id == "ZTEL016").Subject;
        ztel016.Location.SourceTree!.FilePath.Should().Be("b.cs");
    }

    private static string Text(string source, Diagnostic d) =>
        source.Substring(d.Location.SourceSpan.Start, d.Location.SourceSpan.Length);

    private static MetadataReference[] References()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        return trustedPlatformAssemblies
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location))
            .ToArray();
    }

    private static GeneratorDriver RunDriver(string source) =>
        CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(
            CSharpCompilation.Create("TestAssembly", [CSharpSyntaxTree.ParseText(source)], References(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
}
