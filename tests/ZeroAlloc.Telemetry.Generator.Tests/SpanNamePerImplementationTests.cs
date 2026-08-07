using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Covers the <c>{type}</c> span-name token (Telemetry#53).
/// <para>
/// <c>[Trace]</c> is written on the interface method, so without this every implementation of a
/// multi-implementation interface produces the same span name — which is what made
/// <c>[Instrument]</c> unusable on the interfaces most worth tracing.
/// </para>
/// </summary>
public class SpanNamePerImplementationTests
{
    [Fact]
    public Task ResolvesSpanNameFromImplementation_WhenNameContainsTypeToken()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp.VectorStore")]
            public interface IVectorStore
            {
                [Trace("vectorstore.search.{type}")]
                Task<IReadOnlyList<string>> SearchAsync(string collection, CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    /// <summary>
    /// A name that is nothing but the token must not emit <c>"" + _implName + ""</c>.
    /// </summary>
    [Fact]
    public Task EmitsBareImplName_WhenNameIsOnlyTheToken()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            [Instrument("MyApp.Store")]
            public interface IStore
            {
                [Trace("{type}")]
                Task SaveAsync();
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    /// <summary>
    /// Overloads share a method name, so the resolved-name fields must not collide.
    /// </summary>
    [Fact]
    public Task GeneratesDistinctFields_ForOverloadsAndMixedNames()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            [Instrument("MyApp.Store")]
            public interface IStore
            {
                [Trace("store.save.{type}")]
                Task SaveAsync(string key);

                [Trace("store.save.{type}")]
                Task SaveAsync(string key, int ttl);

                [Trace("store.constant")]
                Task PurgeAsync();
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    /// <summary>
    /// The token may sit anywhere, including mid-name and more than once.
    /// </summary>
    [Fact]
    public Task SubstitutesEveryOccurrence_RegardlessOfPosition()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            [Instrument("MyApp.Store")]
            public interface IStore
            {
                [Trace("{type}.save")]
                Task SaveAsync();

                [Trace("a.{type}.b.{type}.c")]
                Task LoadAsync();
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    /// <summary>
    /// A generated proxy whose span name varies by implementation still has to compile.
    /// </summary>
    [Fact]
    public void GeneratedOutput_Compiles()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp.VectorStore")]
            public interface IVectorStore
            {
                [Trace("vectorstore.search.{type}")]
                [TraceTagFromResult("vectorstore.result.count", "Count")]
                Task<IReadOnlyList<string>> SearchAsync(
                    [TraceTag("vectorstore.collection")] string collection,
                    CancellationToken ct);

                [Trace("{type}")]
                Task WarmAsync();

                [Trace("vectorstore.purge")]
                Task PurgeAsync();
            }
            """;

        var errors = CompileWithGenerator(source);
        Assert.Empty(errors);
    }

    private static string[] CompileWithGenerator(string source)
    {
        var parseOptions = new CSharpParseOptions(documentationMode: DocumentationMode.None);
        var compilation = CSharpCompilation.Create("CompileProbeAssembly",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            RuntimeReferences(),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        CSharpGeneratorDriver
            .Create(new InstrumentGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        // foreach rather than LINQ: GetDiagnostics returns an ImmutableArray, and Where on it
        // trips EPS06 (hidden struct copy).
        var errors = new List<string>();
        foreach (var d in output.GetDiagnostics())
        {
            if (d.Severity == DiagnosticSeverity.Error)
                errors.Add(d.ToString());
        }

        return errors.ToArray();
    }

    private static IEnumerable<MetadataReference> RuntimeReferences()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        return trustedPlatformAssemblies
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => MetadataReference.CreateFromFile(p))
            .Concat<MetadataReference>(
            [
                MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location),
            ]);
    }

    private static GeneratorDriver RunGenerator(string source)
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            RuntimeReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(compilation);
    }
}
