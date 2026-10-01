using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>Covers <c>[MetricTagFromResult]</c> (issue #150).</summary>
public class MetricTagTests
{
    /// <summary>
    /// A Result that is a class: every read is null-safe through <c>_tagged</c>. One tag goes on all
    /// four instrument kinds, a second is restricted by <c>Metric</c> to one of them, and the
    /// unguarded histogram's throw path records without tags.
    /// </summary>
    [Fact]
    public void GeneratesTagLists_ForEveryMetric_AndRespectsTheMetricFilter()
    {
        GeneratorSnapshot.Verify(RunGenerator(ResultSource));
    }

    /// <summary>
    /// A struct result, so reads use <c>.</c> on the root; a non-nullable value-type tag, added
    /// without a pattern; and the result itself as a tag through an empty member.
    /// </summary>
    [Fact]
    public void GeneratesTagLists_ForStructAndPlainReturns()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            public readonly struct Quote
            {
                public bool Priced { get; }
                public decimal Cost { get; }
                public int Region { get; }
                public string? Currency { get; }
            }

            [Instrument("MyApp.Quotes")]
            public interface IQuotes
            {
                [HistogramFromResult("quote.cost", "Cost", When = "Priced")]
                [MetricTagFromResult("quote.region", "Region")]
                [MetricTagFromResult("quote.currency", "Currency", When = "Priced")]
                ValueTask<Quote> QuoteAsync(CancellationToken ct);

                [Count("status.calls")]
                [MetricTagFromResult("status", "")]
                string Status();
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    /// <summary>
    /// The tag list is built inside an <c>Enabled</c> check on the instrument, so a call with no
    /// listener neither reads the member nor boxes it.
    /// </summary>
    [Fact]
    public void BuildsTheTagList_OnlyWhenTheInstrumentIsEnabled()
    {
        var generated = RunGeneratorSource(ResultSource);

        // Enabled comes first, so the value, its guard and the tags are all skipped without it.
        Assert.Contains("if (_llm_tokens_input.Enabled && ", generated, StringComparison.Ordinal);
        Assert.Contains("if (_llm_cost.Enabled && ", generated, StringComparison.Ordinal);
        Assert.Contains("if (_llm_calls.Enabled)", generated, StringComparison.Ordinal);
        Assert.Contains("if (_llm_duration.Enabled)", generated, StringComparison.Ordinal);
        // Four on success, and the duration again on the throw path, tagged with error.type (#184).
        Assert.Equal(5, CountOccurrences(generated, "if (_llm_"));
        Assert.Equal(5, CountOccurrences(generated, "new TagList()"));
    }

    /// <summary>
    /// Without <c>[MetricTagFromResult]</c> the success path builds no tag list. The throw path
    /// builds one, for error.type (#184).
    /// </summary>
    [Fact]
    public void EmitsNoTagList_WithoutMetricTags()
    {
        var generated = RunGeneratorSource(ResultSource.Replace("[MetricTagFromResult", "//", StringComparison.Ordinal));

        Assert.Equal(1, CountOccurrences(generated, "new TagList()"));
        Assert.Contains("_metricTags0.Add(\"error.type\", _ex.GetType().FullName);", generated, StringComparison.Ordinal);
        // The only other Enabled reads are the no-listener fast path's, which guards no tag list.
        Assert.Equal(1, CountOccurrences(generated, "if (_llm_"));
        Assert.Contains("if (_llm_duration.Enabled)", generated, StringComparison.Ordinal);
    }

    private const string ResultSource = """
        using ZeroAlloc.Telemetry;
        using System.Threading;
        using System.Threading.Tasks;

        public sealed class LlmError { }

        public sealed class Result<T, E>
        {
            public bool IsSuccess { get; }
            public T Value { get; } = default!;
        }

        public sealed class Reply
        {
            public string? Model { get; set; }
            public int Input { get; set; }
            public decimal Cost { get; set; }
            public bool Cached { get; set; }
        }

        [Instrument("MyApp.Llm")]
        public interface IChat
        {
            [Trace("llm.complete")]
            [Count("llm.calls")]
            [Histogram("llm.duration")]
            [CountFromResult("llm.tokens.input", "Value.Input", When = "IsSuccess")]
            [HistogramFromResult("llm.cost", "Value.Cost", When = "IsSuccess")]
            [MetricTagFromResult("gen_ai.response.model", "Value.Model", When = "IsSuccess")]
            [MetricTagFromResult("llm.cached", "Value.Cached", When = "IsSuccess", Metric = "llm.cost")]
            Task<Result<Reply, LlmError>> CompleteAsync(string prompt, CancellationToken ct);
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
