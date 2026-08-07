using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Covers span tags — <c>[TraceTag]</c> on parameters and <c>[TraceTagFromResult]</c> on methods
/// (Telemetry#30) — plus the opt-in public proxy.
/// </summary>
public class TraceTagTests
{
    [Fact]
    public Task GeneratesSetTag_ForTaggedParameters()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp.VectorStore")]
            public interface IVectorStore
            {
                [Trace("vectorstore.search")]
                Task<IReadOnlyList<string>> SearchAsync(
                    [TraceTag("vectorstore.collection")] string collection,
                    [TraceTag("top.k")] int topK,
                    CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesSetTag_ForResultMember()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp.VectorStore")]
            public interface IVectorStore
            {
                [Trace("vectorstore.search")]
                [TraceTagFromResult("vectorstore.result.count", "Count")]
                Task<IReadOnlyList<string>> SearchAsync(string collection, CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesMultipleResultTags_AndWholeResultTag()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class RerankOutcome
            {
                public int Candidates { get; set; }
                public int Kept { get; set; }
            }

            [Instrument("MyApp.Rerank")]
            public interface IReranker
            {
                [Trace("rerank.run")]
                [TraceTagFromResult("rerank.candidate.count", "Candidates")]
                [TraceTagFromResult("rerank.result.count", "Kept")]
                Task<RerankOutcome> RerankAsync(CancellationToken ct);

                [Trace("rerank.score")]
                [TraceTagFromResult("rerank.score")]
                Task<double> ScoreAsync(CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    /// <summary>
    /// A non-nullable value-type result must use <c>.</c> not <c>?.</c> for member access —
    /// the null-conditional form does not compile against a value type, so getting this wrong
    /// breaks the consumer's build rather than merely losing a tag.
    /// </summary>
    [Fact]
    public Task GeneratesPlainMemberAccess_ForValueTypeResult()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp.Graph")]
            public interface IGraph
            {
                [Trace("graph.cluster")]
                [TraceTagFromResult("graph.community.count", "Length")]
                Task<ReadOnlyMemory<byte>> ClusterAsync(CancellationToken ct);

                [Trace("graph.count")]
                [TraceTagFromResult("graph.node.count", "Value")]
                Task<int?> CountAsync(CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesPublicProxy_WhenPublicProxyRequested()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp.Shared", PublicProxy = true)]
            public interface ISharedService
            {
                [Trace("shared.run")]
                ValueTask RunAsync(CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesNoTags_WhenMethodHasNoTrace()
    {
        // Tags need a span. Without [Trace] nothing is emitted (and ZTEL004 is reported).
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IPlainService
            {
                [Count("plain.calls")]
                Task<string> RunAsync([TraceTag("plain.arg")] string arg, CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
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
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(compilation);
    }
}
