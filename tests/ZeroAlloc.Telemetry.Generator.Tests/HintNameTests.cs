using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Every generated file is named after the instrumented interface's namespace, containing types
/// and generic arity, so two interfaces never share a hint name. A shared hint name makes
/// <c>AddSource</c> throw, and the generator then drops every proxy in the project with CS8785.
/// </summary>
public class HintNameTests
{
    private static GeneratorDriverRunResult Run(string source)
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        var references = trustedPlatformAssemblies
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create("HintNameProbe",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(new InstrumentGenerator())
            .RunGenerators(compilation)
            .GetRunResult();
    }

    private static string[] HintNames(string source)
    {
        var result = Run(source);
        result.Diagnostics.Should().BeEmpty();
        return result.Results[0].GeneratedSources.Select(s => s.HintName).OrderBy(n => n, StringComparer.Ordinal).ToArray();
    }

    [Fact]
    public void NamespaceAndTypeNameNeverRunTogether()
    {
        // Joined with '_', both files were named A_B_CInstrumented.g.cs.
        const string source = """
            using ZeroAlloc.Telemetry;
            namespace A_B { [Instrument("a")] public interface IC { void Run(); } }
            namespace A { [Instrument("b")] public interface IB_C { void Run(); } }
            """;

        HintNames(source).Should().Equal("A.IB_C.Instrumented.g.cs", "A_B.IC.Instrumented.g.cs");
        GeneratorCompilation.OutputErrors(source).Should().BeEmpty();
    }

    [Fact]
    public void GlobalNamespaceHasNoNamespacePart()
    {
        HintNames("""
            using ZeroAlloc.Telemetry;
            [Instrument("a")] public interface IOrders { void Run(); }
            """).Should().Equal("IOrders.Instrumented.g.cs");
    }

    [Fact]
    public void ContainingTypesAndArityArePartOfTheName()
    {
        // Nested and generic interfaces are not supported by the proxy itself yet, but their
        // files must not collide: a collision drops every other proxy in the project.
        var result = Run("""
            using ZeroAlloc.Telemetry;
            namespace App
            {
                public class O1 { [Instrument("a")] public interface IFoo { void Run(); } }
                public class O2<T> { [Instrument("a")] public interface IFoo { void Run(); } }
                [Instrument("a")] public interface IFoo<T> { void Run(); }
                [Instrument("a")] public interface IBar { void Run(); }
            }
            """);

        result.Diagnostics.Should().BeEmpty();
        result.Results[0].GeneratedSources.Select(s => s.HintName).Should().BeEquivalentTo(
            "App.O1+IFoo.Instrumented.g.cs",
            "App.O2`1+IFoo.Instrumented.g.cs",
            "App.IFoo`1.Instrumented.g.cs",
            "App.IBar.Instrumented.g.cs");
    }

    [Theory]
    [InlineData("Plain.Name`1+Inner", "Plain.Name`1+Inner")]
    [InlineData("a b", "a-u0020b")]
    [InlineData("x-y", "x-u002Dy")]
    [InlineData("été", "été")]
    [InlineData("\U0001D400", "\U0001D400")]
    public void Sanitize_KeepsIdentifierCharactersAndEscapesTheRest(string input, string expected)
    {
        Generator.HintNames.Sanitize(input).Should().Be(expected);
    }

    [Fact]
    public void Sanitize_EscapesALoneSurrogate()
    {
        // Built at run time: xUnit cannot carry a lone surrogate through [InlineData].
        var input = new string(['a', (char)0xD800, 'b']);

        Generator.HintNames.Sanitize(input).Should().Be("a-uD800b");
    }
}
