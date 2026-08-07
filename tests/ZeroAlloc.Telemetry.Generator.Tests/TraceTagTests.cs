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

    /// <summary>
    /// Guards the dotted-path defect: emitting <c>?.</c> only on the first segment leaves every
    /// later one unguarded, so a null part-way along the path throws from instrumentation.
    /// The operator for each segment is chosen from the resolved type.
    /// </summary>
    [Fact]
    public Task GeneratesNullSafeAccess_ForEverySegmentOfADottedPath()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public readonly struct Extent { public int Width { get; } }
            public sealed class Inner { public IReadOnlyList<string>? Items { get; set; } }
            public sealed class Outer
            {
                public Inner? Inner { get; set; }
                public int Total { get; set; }
                public Extent Extent { get; set; }
            }

            [Instrument("MyApp.Paths")]
            public interface IPaths
            {
                // Outer? -> Inner? -> IReadOnlyList<string>? -> int : nullable at every hop
                // except the last, so every operator but the final one must be null-safe.
                [Trace("paths.deep")]
                [TraceTagFromResult("deep.count", "Inner.Items.Count")]
                Task<Outer> DeepAsync(CancellationToken ct);

                // A non-nullable value type mid-path must use plain access — ?. against it
                // does not compile.
                [Trace("paths.value")]
                [TraceTagFromResult("value.total", "Total")]
                [TraceTagFromResult("value.width", "Extent.Width")]
                Task<Outer> ValueAsync(CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    /// <summary>
    /// Covers <c>[TraceTagConstant]</c> (issue #36) — the discriminator tags that say which
    /// implementation ran, and the constant-valued attributes the OTel semantic conventions
    /// require. Exercises each constant kind, because each is rendered differently.
    /// </summary>
    [Fact]
    public Task GeneratesSetTag_ForConstantTags()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            public enum SearchMode { Local = 1, Global = 2 }

            [Instrument("MyApp.Rerank")]
            public interface IReranker
            {
                [Trace("rerank.run")]
                [TraceTagConstant("reranker.type", "CohereReranker")]
                [TraceTagConstant("gen_ai.operation.name", "chat")]
                [TraceTagConstant("vectorstore.hybrid", true)]
                [TraceTagConstant("rerank.max", 42)]
                [TraceTagConstant("graphrag.search.mode", SearchMode.Global)]
                Task<string> RerankAsync(CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    /// <summary>
    /// A tag value containing a quote or backslash must be escaped as the language would write
    /// it, or the generated file does not compile.
    /// </summary>
    [Fact]
    public Task GeneratesEscapedLiteral_ForAwkwardConstantValues()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp.Awkward")]
            public interface IAwkward
            {
                [Trace("awkward.run")]
                [TraceTagConstant("quote", "he said \"hi\"")]
                [TraceTagConstant("path", "C:\\temp\\x")]
                Task<string> RunAsync(CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    /// <summary>
    /// Covers <c>[TraceTag(name, member)]</c> (issue #35). Uses the shapes the issue measured as
    /// the largest inexpressible category — counts on collection parameters, and a dotted path
    /// into a value-object id.
    /// </summary>
    [Fact]
    public Task GeneratesSetTag_ForParameterMemberPaths()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class DocumentId { public string Value { get; set; } = ""; }
            public sealed class DocumentMetadata
            {
                public DocumentId? DocumentId { get; set; }
                public string? ContentType { get; set; }
            }

            [Instrument("MyApp.Ingest")]
            public interface IIngest
            {
                [Trace("ingest.store")]
                Task StoreAsync(
                    [TraceTag("document.id", "DocumentId.Value")] DocumentMetadata metadata,
                    [TraceTag("vectorstore.batch.size", "Count")] IReadOnlyList<string> chunks,
                    CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    /// <summary>
    /// A member path on a non-nullable value-type parameter must not null-test the argument:
    /// there is nothing to test, and a copy would be noise.
    /// </summary>
    [Fact]
    public Task GeneratesPlainAccess_ForValueTypeParameter()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp.Spans")]
            public interface ISpans
            {
                [Trace("spans.take")]
                Task TakeAsync([TraceTag("span.length", "Length")] ReadOnlyMemory<byte> buffer, CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    /// <summary>
    /// Covers <c>When</c> (issue #37) — a tag whose value is only valid on one branch of a
    /// Result-style return. The guard must prevent the member being read at all, which a
    /// null-conditional cannot do.
    /// </summary>
    [Fact]
    public Task GeneratesGuardedSetTag_ForConditionalResultTags()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class RagError { }

            public sealed class Result<T, E>
            {
                public bool IsSuccess { get; }
                public T Value { get; } = default!;
            }

            public sealed class Chunk { }

            [Instrument("MyApp.Ingest")]
            public interface IIngest
            {
                [Trace("ingest.chunk")]
                [TraceTagFromResult("chunk.count", "Value.Count", When = "IsSuccess")]
                Task<Result<IReadOnlyList<Chunk>, RagError>> ChunkAsync(CancellationToken ct);
            }
            """;

        return Verifier.Verify(RunGenerator(source));
    }

    /// <summary>
    /// A guard on a non-nullable value-type result needs no null-tolerant comparison — the bare
    /// boolean reads better and some analyzers flag <c>x == true</c>.
    /// </summary>
    [Fact]
    public Task GeneratesBareGuard_ForNonNullableBool()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            public readonly struct Outcome
            {
                public bool Ok { get; }
                public int Count { get; }
            }

            [Instrument("MyApp.Outcome")]
            public interface IOutcome
            {
                [Trace("outcome.run")]
                [TraceTagFromResult("outcome.count", "Count", When = "Ok")]
                ValueTask<Outcome> RunAsync(CancellationToken ct);
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
