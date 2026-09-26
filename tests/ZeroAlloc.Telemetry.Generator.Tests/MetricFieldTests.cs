using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Metric fields: one static field per instrument kind and metric name, each with a valid and
/// distinct identifier, and each name emitted as an escaped literal.
/// </summary>
public class MetricFieldTests
{
    private const string CollidingSource = """
        using ZeroAlloc.Telemetry;
        using System.Threading;
        using System.Threading.Tasks;

        [Instrument("MyApp.Fields")]
        public interface IFields
        {
            // Same name, different kinds: 1.6.4 emitted only the counter, so the histogram
            // field was missing and the proxy did not compile.
            [Count("x")]
            Task<int> CountedAsync(CancellationToken ct);

            [Histogram("x")]
            Task<int> TimedAsync(CancellationToken ct);

            // Distinct names that sanitize to the same identifier.
            [Count("a.b")]
            Task<int> DottedAsync(CancellationToken ct);

            [Count("a_b")]
            Task<int> UnderscoredAsync(CancellationToken ct);

            // Characters that are not valid in an identifier, and a quote that must be escaped.
            [Count("http/requests total")]
            Task<int> SlashAsync(CancellationToken ct);

            [Count("say \"hi\"")]
            Task<int> QuotedAsync(CancellationToken ct);

            // Names that would collide with the proxy's own members and locals.
            [Count("meter")]
            Task<int> MeterAsync(CancellationToken ct);

            [Histogram("result")]
            Task<int> ResultAsync(CancellationToken ct);

            // Same kind and name on another method: still one field.
            [Count("x")]
            Task<int> CountedAgainAsync(CancellationToken ct);
        }
        """;

    [Fact]
    public void GeneratesDistinctFields_ForCollidingMetricNames()
        => GeneratorSnapshot.Verify(RunGenerator(CollidingSource));

    private const string UnitSource = """
        using ZeroAlloc.Telemetry;
        using System.Threading;
        using System.Threading.Tasks;

        [Instrument("MyApp.Orders")]
        public interface IOrders
        {
            [Count("orders.created", Unit = "{order}", Description = "Orders accepted for fulfilment")]
            [Histogram("order.create_ms", Unit = "ms")]
            Task<int> CreateAsync(CancellationToken ct);

            // Shares the counter. Only the first non-null Unit and Description are used, so this
            // Description is ignored and the field above is unchanged.
            [Count("orders.created", Description = "ignored: a description was already declared")]
            [Histogram("order.create_ms", Description = "Time to create an order")]
            Task<int> CreateAgainAsync(CancellationToken ct);
        }
        """;

    [Fact]
    public void GeneratesUnitAndDescription_ForCountAndHistogram()
        => GeneratorSnapshot.Verify(RunGenerator(UnitSource));

    [Fact]
    public void PassesTheFirstUnitAndDescription_ToTheFactory()
    {
        var generated = RunGeneratorSource(UnitSource);

        Assert.Contains(
            "Counter<long> _orders_created = _meter.CreateCounter<long>(\"orders.created\", unit: \"{order}\", description: \"Orders accepted for fulfilment\");",
            generated, StringComparison.Ordinal);
        Assert.Contains(
            "Histogram<double> _order_create_ms = _meter.CreateHistogram<double>(\"order.create_ms\", unit: \"ms\", description: \"Time to create an order\");",
            generated, StringComparison.Ordinal);
        Assert.DoesNotContain("ignored:", generated, StringComparison.Ordinal);
    }

    /// <summary>
    /// Asserts the field block directly, so a snapshot approved while wrong cannot lock the
    /// collision back in.
    /// </summary>
    [Fact]
    public void EmitsOneFieldPerKindAndName()
    {
        var generated = RunGeneratorSource(CollidingSource);

        Assert.Contains("Counter<long> _x = _meter.CreateCounter<long>(\"x\");", generated, StringComparison.Ordinal);
        Assert.Contains("Histogram<double> _x_2 = _meter.CreateHistogram<double>(\"x\");", generated, StringComparison.Ordinal);
        Assert.Contains("Counter<long> _a_b = _meter.CreateCounter<long>(\"a.b\");", generated, StringComparison.Ordinal);
        Assert.Contains("Counter<long> _a_b_2 = _meter.CreateCounter<long>(\"a_b\");", generated, StringComparison.Ordinal);
        Assert.Contains("Counter<long> _http_requests_total = _meter.CreateCounter<long>(\"http/requests total\");", generated, StringComparison.Ordinal);
        Assert.Contains("Counter<long> _say__hi_ = _meter.CreateCounter<long>(\"say \\\"hi\\\"\");", generated, StringComparison.Ordinal);
        Assert.Contains("Counter<long> _metric_meter = _meter.CreateCounter<long>(\"meter\");", generated, StringComparison.Ordinal);
        Assert.Contains("Histogram<double> _metric_result = _meter.CreateHistogram<double>(\"result\");", generated, StringComparison.Ordinal);
        Assert.Contains("_metric_result.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);", generated, StringComparison.Ordinal);

        // Two [Count("x")] methods, one field.
        Assert.Equal(1, CountOccurrences(generated, "Counter<long> _x ="));
    }

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
