using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Covers <c>[CountFromResult]</c>, <c>[HistogramFromResult]</c> and <c>When</c> on
/// <c>[Count]</c>/<c>[Histogram]</c> (issue #142).
/// </summary>
public class ResultMetricTests
{
    /// <summary>
    /// A Result that is a class: nullable at every hop, so every read is null-safe, and a span tag
    /// and three instruments share one <c>_tagged</c> copy. Also covers two counters on one
    /// method, a <c>long?</c>, a <c>decimal</c>, and <c>When</c>, <c>Unit</c> and <c>Description</c>.
    /// </summary>
    [Fact]
    public void GeneratesResultMetrics_ForResultReturn()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class LlmError { }

            public sealed class Result<T, E>
            {
                public bool IsSuccess { get; }
                public T Value { get; } = default!;
            }

            public sealed class TokenUsage
            {
                public int Input { get; set; }
                public long? Output { get; set; }
                public decimal Cost { get; set; }
            }

            [Instrument("MyApp.Llm")]
            public interface IChat
            {
                [Trace("llm.complete")]
                [TraceTagFromResult("gen_ai.usage.input_tokens", "Value.Input", When = "IsSuccess")]
                [CountFromResult("llm.tokens.input", "Value.Input", When = "IsSuccess", Unit = "{token}", Description = "Prompt tokens consumed")]
                [CountFromResult("llm.tokens.output", "Value.Output", When = "IsSuccess", Unit = "{token}")]
                [HistogramFromResult("llm.cost", "Value.Cost", When = "IsSuccess", Unit = "USD", Description = "Cost of the call")]
                Task<Result<TokenUsage, LlmError>> CompleteAsync(string prompt, CancellationToken ct);
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    /// <summary>
    /// <c>When</c> on <c>[Count]</c> and <c>[Histogram]</c>. A guarded histogram records on the
    /// success path only: a throw leaves no result to evaluate the guard against.
    /// </summary>
    [Fact]
    public void GeneratesGuardedCountAndHistogram()
    {
        var generated = RunGeneratorSource(GuardedSource);

        // One Record, in the try. The catch sets the span status and rethrows, nothing more.
        Assert.Equal(1, CountOccurrences(generated, "_orders_accept_ms.Record("));
        GeneratorSnapshot.Verify(RunGenerator(GuardedSource));
    }

    /// <summary>
    /// Plain returns rather than a Result: a struct root with a bare guard and a decimal cast, a
    /// class root without a guard, an empty member reading the value itself, and a nullable int
    /// read through Value.
    /// </summary>
    [Fact]
    public void GeneratesResultMetrics_ForPlainAndValueTypeReturns()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            public readonly struct Quote
            {
                public bool Priced { get; }
                public decimal Cost { get; }
            }

            public sealed class SearchPage { public int Hits { get; set; } }

            [Instrument("MyApp.Plain")]
            public interface IPlain
            {
                [HistogramFromResult("quote.cost", "Cost", When = "Priced")]
                ValueTask<Quote> QuoteAsync(CancellationToken ct);

                [HistogramFromResult("search.hits", "Hits")]
                Task<SearchPage> SearchAsync(CancellationToken ct);

                [CountFromResult("batch.items", "")]
                Task<int> BatchAsync(CancellationToken ct);

                [CountFromResult("maybe.items", "Value")]
                Task<int?> MaybeAsync(CancellationToken ct);
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    private const string GuardedSource = """
        using ZeroAlloc.Telemetry;
        using System.Threading;
        using System.Threading.Tasks;

        public sealed class OrderError { }

        public sealed class Result<T, E>
        {
            public bool IsSuccess { get; }
            public T Value { get; } = default!;
        }

        [Instrument("MyApp.Orders")]
        public interface IOrders
        {
            [Trace("orders.accept")]
            [Count("orders.accepted", When = "IsSuccess")]
            [Histogram("orders.accept_ms", When = "IsSuccess", Unit = "ms")]
            Task<Result<string, OrderError>> AcceptAsync(string orderId, CancellationToken ct);
        }
        """;

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
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
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(compilation);
    }
}
