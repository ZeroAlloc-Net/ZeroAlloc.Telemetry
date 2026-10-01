using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// The metric features the OpenTelemetry GenAI conventions need (#171): a histogram unit that
/// decides what is recorded, bucket boundaries, constant and parameter metric tags, and a
/// per-element histogram.
/// </summary>
public class MetricConventionsTests
{
    [Theory]
    [InlineData(null, "TotalMilliseconds")]
    [InlineData("ms", "TotalMilliseconds")]
    [InlineData("s", "TotalSeconds")]
    [InlineData("us", "TotalMicroseconds")]
    [InlineData("ns", "TotalNanoseconds")]
    [InlineData("min", "TotalMinutes")]
    [InlineData("h", "TotalHours")]
    public void HistogramUnit_DecidesTheElapsedValue_OnBothPaths(string? unit, string member)
    {
        var unitArgument = unit is null ? string.Empty : $", Unit = \"{unit}\"";
        var run = GeneratorCompilation.RunAll($$"""
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [Histogram("op.duration"{{unitArgument}})]
                Task<int> RunAsync();
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        var proxy = Proxy(run, "IOps");
        CountOccurrences(proxy, "Stopwatch.GetElapsedTime(_sw)." + member + ")").Should().Be(2);
    }

    [Fact]
    public void HistogramUnit_ThatIsNotATimeUnit_ReportsZtel018_AndRecordsMilliseconds()
    {
        const string source = """
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [Histogram("op.duration", Unit = "{request}")]
                Task<int> RunAsync();
            }
            """;
        var run = GeneratorCompilation.RunAll(source);

        var d = run.GeneratorDiagnostics.Should().ContainSingle().Subject;
        d.Id.Should().Be("ZTEL018");
        d.Severity.Should().Be(DiagnosticSeverity.Warning);
        d.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "[Histogram(\"op.duration\")] has Unit = '{request}', which is not a time unit the generator converts to, so durations are recorded in milliseconds under the unit '{request}'. Use ms, s, us, ns, min or h.");
        Text(source, d).Should().Be("\"{request}\"");
        run.Errors.Should().BeEmpty();
        Proxy(run, "IOps").Should().Contain("Stopwatch.GetElapsedTime(_sw).TotalMilliseconds");
    }

    [Fact]
    public void Buckets_ArePassedAsInstrumentAdvice_OnEitherHistogram()
    {
        var run = GeneratorCompilation.RunAll("""
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            public sealed class Reply { public int Tokens { get; set; } }
            [Instrument("a")]
            public interface IOps
            {
                [Histogram("op.duration", Unit = "s", Buckets = new[] { 0.01, 0.02, 0.04, 1.5E-1, 10d })]
                [HistogramFromResult("op.tokens", "Tokens", Buckets = new double[] { 1, 4, 16 }, Description = "tokens")]
                Task<Reply> RunAsync();
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        var proxy = Proxy(run, "IOps");
        proxy.Should().Contain(
            "_meter.CreateHistogram<double>(\"op.duration\", unit: \"s\", description: null, tags: null, advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = new double[] { 0.01, 0.02, 0.04, 0.15, 10 } });");
        proxy.Should().Contain(
            "_meter.CreateHistogram<double>(\"op.tokens\", unit: null, description: \"tokens\", tags: null, advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = new double[] { 1, 4, 16 } });");
    }

    /// <summary>Several methods record one metric: the first that declares buckets sets them.</summary>
    [Fact]
    public void Buckets_OnAMetricRecordedByTwoMethods_TheFirstDeclarationWins()
    {
        var run = GeneratorCompilation.RunAll("""
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [Histogram("op.duration")] void A();
                [Histogram("op.duration", Buckets = new[] { 1d, 2d })] void B();
                [Histogram("op.duration", Buckets = new[] { 5d })] void C();
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        Proxy(run, "IOps").Should().Contain("HistogramBucketBoundaries = new double[] { 1, 2 }");
    }

    [Theory]
    [InlineData("new double[0]", "are empty")]
    [InlineData("new[] { 1d, 1d }", "are not in strictly increasing order: 1 follows 1")]
    [InlineData("new[] { 2d, 1d }", "are not in strictly increasing order: 1 follows 2")]
    [InlineData("new[] { 1d, double.NaN }", "contain a value that is not finite")]
    [InlineData("new[] { 1d, double.PositiveInfinity }", "contain a value that is not finite")]
    public void InvalidBuckets_ReportZtel019_AndTheInstrumentGetsNoAdvice(string buckets, string problem)
    {
        var source = $$"""
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [Histogram("op.duration", Buckets = {{buckets}})] void Run();
            }
            """;
        var run = GeneratorCompilation.RunAll(source);

        var d = run.GeneratorDiagnostics.Should().ContainSingle().Subject;
        d.Id.Should().Be("ZTEL019");
        d.Severity.Should().Be(DiagnosticSeverity.Error);
        d.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"Buckets on [Histogram(\"op.duration\")] {problem}. Bucket boundaries must be finite and in strictly increasing order.");
        Text(source, d).Should().Be(buckets);
        run.Errors.Should().BeEmpty();
        Proxy(run, "IOps").Should().NotContain("InstrumentAdvice");
    }

    /// <summary>
    /// Without System.Diagnostics.DiagnosticSource 9.0 there is no <c>InstrumentAdvice</c>, so the
    /// buckets are reported rather than emitted into code that would not compile.
    /// </summary>
    [Fact]
    public void Buckets_WithoutInstrumentAdvice_ReportZtel020()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [HistogramFromResult("op.size", "", Buckets = new[] { 1d })] int Run();
            }
            """;
        var references = GeneratorTestReferences.All()
            .Where(r => !(r.Display ?? string.Empty).EndsWith("System.Diagnostics.DiagnosticSource.dll", StringComparison.OrdinalIgnoreCase));
        var compilation = CSharpCompilation.Create("NoAdvice",
            [CSharpSyntaxTree.ParseText(source)],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = CSharpGeneratorDriver.Create(new InstrumentGenerator())
            .RunGenerators(compilation).GetRunResult().Diagnostics;

        var d = diagnostics.Should().ContainSingle().Subject;
        d.Id.Should().Be("ZTEL020");
        d.GetMessage(CultureInfo.InvariantCulture).Should().StartWith(
            "Buckets on [HistogramFromResult(\"op.size\")] need System.Diagnostics.Metrics.InstrumentAdvice<T>");
    }

    [Fact]
    public void ConstantAndParameterTags_AreBuiltBehindEnabled_OnEveryMetric_AndTheThrowPath()
    {
        var run = GeneratorCompilation.RunAll("""
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            public enum Modality { Text = 1, Image = 2 }
            public sealed class Request { public string? Model { get; set; } public Options Options { get; set; } = new(); }
            public sealed class Options { public int Temperature { get; set; } }
            public sealed class Reply { public int Tokens { get; set; } public string? Model { get; set; } }
            [Instrument("a")]
            public interface IChat
            {
                [Histogram("gen_ai.client.operation.duration", Unit = "s")]
                [HistogramFromResult("gen_ai.client.token.usage", "Tokens", Unit = "{token}")]
                [MetricTagConstant("gen_ai.operation.name", "chat")]
                [MetricTagConstant("gen_ai.provider.name", "openai")]
                [MetricTagConstant("gen_ai.token.modality", Modality.Text, Metric = "gen_ai.client.token.usage")]
                [MetricTagConstant("ignored", null)]
                [MetricTagFromResult("gen_ai.response.model", "Model")]
                Task<Reply> CompleteAsync(
                    [MetricTag("gen_ai.request.model", "Model")] Request request,
                    [MetricTag("temperature", "Options.Temperature", Metric = "gen_ai.client.operation.duration")] Request again,
                    [MetricTag("region")] string? region,
                    CancellationToken ct);
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        var proxy = Proxy(run, "IChat");

        // The token histogram carries every tag; the modality constant is restricted to it.
        proxy.Should().Contain("_metricTags0.Add(\"gen_ai.token.modality\", (global::Modality)1);");
        proxy.Should().Contain("if (request?.Model is { } _metricTag0_3)");
        proxy.Should().Contain("if (region is { } _metricTag0_4)");
        proxy.Should().Contain("if (_tagged?.Model is { } _metricTag0_5)");

        // The duration carries the temperature, restricted to it, and not the modality.
        proxy.Should().Contain("_metricTags1.Add(\"gen_ai.operation.name\", \"chat\");");
        proxy.Should().Contain("if (again?.Options?.Temperature is { } _metricTag1_3)");
        proxy.Should().NotContain("_metricTags1.Add(\"gen_ai.token.modality\"");
        proxy.Should().NotContain("\"ignored\"");

        // The throw path: the unguarded duration carries the tags that do not read the result.
        var catchBlock = proxy[proxy.IndexOf("catch (Exception)", StringComparison.Ordinal)..];
        catchBlock.Should().Contain("_metricTags2.Add(\"gen_ai.operation.name\", \"chat\");");
        catchBlock.Should().Contain("_metricTags2.Add(\"gen_ai.request.model\"");
        catchBlock.Should().NotContain("gen_ai.response.model");
        catchBlock.Should().Contain("_gen_ai_client_operation_duration.Record(Stopwatch.GetElapsedTime(_sw).TotalSeconds, in _metricTags2);");
    }

    /// <summary>The whole GenAI client shape, as one snapshot.</summary>
    [Fact]
    public void GenAiClientMetrics_Snapshot()
    {
        const string source = """
            using System;
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            public readonly struct Usage
            {
                public ReadOnlyMemory<double> Latencies { get; init; }
                public double[] Scores { get; init; }
            }
            public readonly struct Reply { public Usage Usage { get; init; } }
            [Instrument("gen_ai")]
            public interface IChat
            {
                [Histogram("gen_ai.client.operation.duration", Unit = "s", Buckets = new[] { 0.01, 0.1, 1d, 10d })]
                [HistogramFromResult("gen_ai.latency", "Usage.Latencies", Each = true, Unit = "s")]
                [HistogramFromResult("gen_ai.score", "Usage.Scores", Each = true)]
                [MetricTagConstant("gen_ai.operation.name", "chat")]
                ValueTask<Reply> CompleteAsync([MetricTag("gen_ai.request.model")] string model, CancellationToken ct);
            }
            """;

        GeneratorSnapshot.Verify(RunDriver(source));
    }

    [Fact]
    public void Each_OverEverySupportedShape_Compiles_AndIsBehindEnabled()
    {
        var run = GeneratorCompilation.RunAll("""
            using System;
            using System.Collections.Generic;
            using System.Collections.Immutable;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            public readonly struct Shapes
            {
                public ReadOnlySpan<double> Span => default;
                public Span<double> MutableSpan => default;
                public ReadOnlyMemory<double> Memory { get; init; }
                public Memory<double>? MaybeMemory { get; init; }
                public double[]? Array { get; init; }
                public List<double?> List { get; init; }
                public ImmutableArray<decimal> Immutable { get; init; }
                public int[] Ints { get; init; }
            }
            [Instrument("a")]
            public interface IOps
            {
                [HistogramFromResult("span", "Span", Each = true)]
                [HistogramFromResult("mutable", "MutableSpan", Each = true)]
                [HistogramFromResult("memory", "Memory", Each = true)]
                [HistogramFromResult("maybe", "MaybeMemory", Each = true)]
                [HistogramFromResult("array", "Array", Each = true)]
                [HistogramFromResult("list", "List", Each = true)]
                [HistogramFromResult("immutable", "Immutable", Each = true)]
                [HistogramFromResult("ints", "Ints", Each = true)]
                ValueTask<Shapes> RunAsync();

                [HistogramFromResult("sync", "Span", Each = true)]
                [MetricTagConstant("k", 1)]
                Shapes Run();
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        var proxy = Proxy(run, "IOps");
        proxy.Should().Contain("if (_span.Enabled)");
        proxy.Should().Contain("_recordEach(_span, _result.Span);");
        proxy.Should().Contain("_recordEach(_memory, _result.Memory.Span);");
        proxy.Should().Contain("if (_maybe.Enabled && _result.MaybeMemory is { } _read3)");
        proxy.Should().Contain("_recordEach(_maybe, _read3.Span);");
        proxy.Should().Contain("if (_list.Enabled && _result.List is { } _read5)");
        proxy.Should().Contain("foreach (var _each5 in _read5)");
        proxy.Should().Contain("if (_each5 is { } _eachValue5)");
        proxy.Should().Contain("_immutable.Record((double)_each6);");
        proxy.Should().Contain("_recordEach(_sync, _result.Span, in _metricTags0);");
    }

    [Theory]
    [InlineData("Plain", "double", "With Each = true the member must be")]
    [InlineData("Enumerable", "System.Collections.Generic.IEnumerable<double>", "With Each = true the member must be")]
    [InlineData("Strings", "string[]", "With Each = true the member must be")]
    [InlineData("Value.Span", "System.ReadOnlySpan<double>", "A span cannot be read through a value that can be null")]
    public void Each_OverAMemberThatCannotBeIteratedFreely_ReportsZtel009(string member, string type, string reason)
    {
        var source = $$"""
            using System;
            using System.Collections.Generic;
            using ZeroAlloc.Telemetry;
            public readonly struct Holder { public ReadOnlySpan<double> Span => default; }
            public sealed class Shapes
            {
                public double Plain { get; init; }
                public IEnumerable<double> Enumerable { get; init; } = [];
                public string[] Strings { get; init; } = [];
                public Holder? Value { get; init; }
            }
            [Instrument("a")]
            public interface IOps
            {
                [HistogramFromResult("m", "{{member}}", Each = true)] Shapes Run();
            }
            """;
        var run = GeneratorCompilation.RunAll(source);

        var d = run.GeneratorDiagnostics.Should().ContainSingle().Subject;
        d.Id.Should().Be("ZTEL009");
        d.GetMessage(CultureInfo.InvariantCulture).Should().StartWith(
            $"[HistogramFromResult(Each = true)] cannot record '{member}' of type '{type}'. {reason}");
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void MetricTag_WithoutAMetric_OrNamingAnUnknownOne_ReportsZtel011()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [MetricTagConstant("op", "x")]
                void None([MetricTag("p")] int value);

                [Count("calls")]
                [MetricTagConstant("op", "x", Metric = "nope")]
                void Unknown([MetricTag("p", Metric = "other")] int value);
            }
            """;
        var run = GeneratorCompilation.RunAll(source);

        var messages = run.GeneratorDiagnostics
            .Select(d => d.Id + " " + d.GetMessage(CultureInfo.InvariantCulture))
            .ToArray();
        messages.Should().BeEquivalentTo(
            "ZTEL011 [MetricTagConstant(\"op\")] on 'IOps.None' records nothing — the method records no metric to carry the tag. Add [Count], [Histogram], [CountFromResult] or [HistogramFromResult], or remove the tag.",
            "ZTEL011 [MetricTag(\"p\")] on 'IOps.None' records nothing — the method records no metric to carry the tag. Add [Count], [Histogram], [CountFromResult] or [HistogramFromResult], or remove the tag.",
            "ZTEL011 [MetricTagConstant(\"op\")] on 'IOps.Unknown' records nothing — Metric = 'nope' names no metric the method records. Name one of its [Count], [Histogram], [CountFromResult] or [HistogramFromResult] metrics, or remove Metric to tag them all.",
            "ZTEL011 [MetricTag(\"p\")] on 'IOps.Unknown' records nothing — Metric = 'other' names no metric the method records. Name one of its [Count], [Histogram], [CountFromResult] or [HistogramFromResult] metrics, or remove Metric to tag them all.");
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void SameTagName_FromDifferentKindsOnOneMetric_ReportsZtel012_AtTheLaterOne()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [Count("calls")]
                [MetricTagConstant("model", "x")]
                [MetricTagFromResult("model", "")]
                string Run([MetricTag("model")] string model);
            }
            """;
        var run = GeneratorCompilation.RunAll(source);

        run.GeneratorDiagnostics.Select(d => d.Id).Should().Equal("ZTEL012", "ZTEL012");
        run.GeneratorDiagnostics[0].GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "[MetricTag] adds the tag 'model' to 'calls', which another metric tag on 'IOps.Run' already adds. A measurement carries one value per tag name, so remove or rename one of them.");
        run.Errors.Should().BeEmpty();
        Proxy(run, "IOps").Should().Contain("Add(\"model\", \"x\")").And.NotContain("Add(\"model\", model)");
    }

    [Fact]
    public void MetricTag_PathThatDoesNotResolve_ReportsZtel010()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            public sealed class Request { public string? Model { get; set; } }
            [Instrument("a")]
            public interface IOps
            {
                [Count("calls")]
                void Run([MetricTag("model", "Modle")] Request request);
            }
            """;
        var run = GeneratorCompilation.RunAll(source);

        var d = run.GeneratorDiagnostics.Should().ContainSingle().Subject;
        d.Id.Should().Be("ZTEL010");
        d.GetMessage(CultureInfo.InvariantCulture).Should().StartWith(
            "'Modle' in the path 'Modle' is not a readable instance property or field of 'Request', so [MetricTag] on 'request' records nothing.");
        Text(source, d).Should().Be("\"Modle\"");
        run.Errors.Should().BeEmpty();
    }

    [Theory]
    [InlineData("MetricTagConstant", "[Count(\"calls\")]")]
    [InlineData("TraceTagConstant", "[Trace(\"run\")]")]
    public void ConstantTag_WhoseValueIsAnArrayOrAType_ReportsZtel021(string attribute, string instrument)
    {
        var source = $$"""
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                {{instrument}}
                [{{attribute}}("array", new[] { 1, 2 })]
                [{{attribute}}("type", typeof(string))]
                void Run();
            }
            """;
        var run = GeneratorCompilation.RunAll(source);

        run.GeneratorDiagnostics.Select(d => d.Id).Should().Equal("ZTEL021", "ZTEL021");
        var d = run.GeneratorDiagnostics[0];
        d.Severity.Should().Be(DiagnosticSeverity.Warning);
        d.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            $"[{attribute}(\"array\", ...)] on 'IOps.Run' records nothing — its value is an array or a type. A constant tag value must be a string, bool, char, number or enum.");
        Text(source, d).Should().Be("new[] { 1, 2 }");
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void MetricTagConstant_OnAnUninstrumentedType_ReportsZtel003()
    {
        var run = GeneratorCompilation.RunAll("""
            using ZeroAlloc.Telemetry;
            public interface IOps { [MetricTagConstant("op", "x")] void Run(); }
            """);

        run.GeneratorDiagnostics.Should().ContainSingle().Which.Id.Should().Be("ZTEL003");
    }

    /// <summary>Interfaces that use none of the new features generate exactly what they did before.</summary>
    [Fact]
    public void WithoutTheNewFeatures_NoAdviceNoHelperAndMilliseconds()
    {
        var run = GeneratorCompilation.RunAll("""
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps { [Histogram("d", Unit = "ms")] [Count("c")] Task<int> RunAsync(); }
            """);

        var proxy = Proxy(run, "IOps");
        proxy.Should().Contain("_meter.CreateHistogram<double>(\"d\", unit: \"ms\");");
        proxy.Should().NotContain("_recordEach").And.NotContain("TagList");
    }

    private static string Proxy(GeneratorOutput run, string interfaceName) =>
        run.Output.SyntaxTrees
            .First(t => t.FilePath.EndsWith(interfaceName + ".Instrumented.g.cs", StringComparison.Ordinal))
            .ToString();

    private static string Text(string source, Diagnostic d) =>
        d.Location.SourceTree!.GetText().ToString(d.Location.SourceSpan);

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static GeneratorDriver RunDriver(string source) =>
        CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(
            CSharpCompilation.Create("TestAssembly", [CSharpSyntaxTree.ParseText(source)], GeneratorTestReferences.All(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
}
