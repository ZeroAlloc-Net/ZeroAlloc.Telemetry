using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Guards Telemetry#29: the proxy must reproduce the interface's nullable reference type
/// annotations exactly.
/// <para>
/// The generated class implements the interface, so a dropped <c>?</c> is not cosmetic — the
/// signatures no longer match and the consumer's build fails with CS8613 (return) or CS8767
/// (parameter), plus CS8603 on the forwarded return. The generator breaks the build of anyone
/// whose interface uses nullable annotations.
/// </para>
/// </summary>
public class NullableAnnotationTests
{
    private const string NullableSource = """
        #nullable enable
        using ZeroAlloc.Telemetry;
        using System.Threading;
        using System.Threading.Tasks;

        public sealed class OrderRow { }

        [Instrument("MyApp.Orders")]
        public interface IOrderRepository
        {
            // Nullable result — the shape reported in Telemetry#29.
            [Trace("orders.get_by_id")]
            Task<OrderRow?> GetByIdAsync(int id, CancellationToken ct);

            // Nullable parameter — the same defect on the other side of the signature.
            [Trace("orders.find")]
            Task<OrderRow> FindAsync(string? filter, CancellationToken ct);

            // Nullable in both positions, and a non-nullable neighbour that must stay bare.
            [Trace("orders.search")]
            Task<OrderRow?> SearchAsync(string? term, string tenant, CancellationToken ct);
        }
        """;

    [Fact]
    public Task PreservesNullableAnnotations_OnReturnsAndParameters()
        => Verifier.Verify(RunGenerator(NullableSource));

    /// <summary>
    /// Asserts the emitted text directly rather than only through the snapshot: a snapshot that
    /// was approved while wrong would lock the bug in, and this failure names the cause.
    /// </summary>
    [Fact]
    public void GeneratedSignatures_MatchTheInterfaceExactly()
    {
        var generated = RunGeneratorSource(NullableSource);

        Assert.Contains("Task<global::OrderRow?> GetByIdAsync(int id,", generated, StringComparison.Ordinal);
        Assert.Contains("FindAsync(string? filter,", generated, StringComparison.Ordinal);
        Assert.Contains("Task<global::OrderRow?> SearchAsync(string? term, string tenant,", generated, StringComparison.Ordinal);
    }

    [Fact]
    public void NonNullableReferenceTypes_AreNotAnnotated()
    {
        // The fix must not over-annotate: a non-nullable reference type stays bare, or every
        // existing consumer signature would stop matching in the opposite direction.
        var generated = RunGeneratorSource(NullableSource);

        Assert.DoesNotContain("string? tenant", generated, StringComparison.Ordinal);
        Assert.Contains("Task<global::OrderRow> FindAsync", generated, StringComparison.Ordinal);
    }

    private static string RunGeneratorSource(string source)
    {
        // Plain loop rather than LINQ: EPS06 flags Select over ImmutableArray as a hidden copy.
        var sb = new System.Text.StringBuilder();
        foreach (var tree in RunGenerator(source).GetRunResult().GeneratedTrees)
            sb.AppendLine(tree.ToString());

        return sb.ToString();
    }

    private static GeneratorDriver RunGenerator(string source)
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        var runtimeRefs = trustedPlatformAssemblies
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => MetadataReference.CreateFromFile(p))
            .ToArray();

        var compilation = CSharpCompilation.Create("TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            runtimeRefs.Concat<MetadataReference>(
            [
                MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location),
            ]),
            // Nullable context on: annotations only carry meaning when the compilation enables them.
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable));

        return CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(compilation);
    }
}
