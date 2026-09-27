using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// The generator's pipeline must serve cached results when nothing it reads has changed. Every
/// value between stages has to be value-equatable for that to happen; one reference-compared
/// collection or <see cref="Diagnostic"/> makes every run look new, and the generator then
/// re-emits every proxy on every keystroke in the IDE.
/// </summary>
public class IncrementalCachingTests
{
    // The generator's WithTrackingName values.
    private const string InstrumentsStep = "Instruments";
    private const string OrphanDiagnosticsStep = "OrphanDiagnostics";

    /// <summary>
    /// Exercises every model field: parameters with tags, result tags with a guard, constant
    /// tags, plain and result-driven metrics, a templated span name, and diagnostics from both
    /// the instrument pipeline and the orphan-attribute pipeline.
    /// </summary>
    private const string InterfaceSource = """
        using ZeroAlloc.Telemetry;
        using System.Threading.Tasks;

        namespace MyApp;

        public sealed class Receipt
        {
            public bool IsSuccess { get; set; }
            public int Items { get; set; }
            public decimal? Total { get; set; }
        }

        [Instrument("MyApp.Orders", PublicProxy = true)]
        public interface IOrderService
        {
            [Trace("order.{type}.create")]
            [TraceTagConstant("channel", "web")]
            [TraceTagFromResult("items", "Items", When = "IsSuccess")]
            [Count("orders.created", When = "IsSuccess")]
            [Histogram("orders.create_ms", Unit = "ms")]
            [CountFromResult("orders.items", "Items")]
            [HistogramFromResult("orders.total", "Total", Description = "Order total")]
            Task<Receipt> CreateAsync([TraceTag("order.id")] string id, [TraceTag("order.len", "Length")] string? note);

            [Trace("order.{Type}.get")]
            [TraceTagFromResult("missing", "Nope")]
            Task<Receipt> GetAsync(string id);

            [TraceTag("x")]
            void Untraced();
        }

        public class Orphan
        {
            [Trace("orphan")]
            public void Run() { }
        }
        """;

    private const string UnrelatedSource = """
        namespace MyApp;

        public static class Unrelated
        {
            public static int Value => 1;
        }
        """;

    [Fact]
    public void UnchangedCompilation_ServesEveryOutputFromCache()
    {
        var compilation = CreateCompilation();
        var driver = RunTwice(compilation, compilation.Clone());

        AssertAllCached(driver);
    }

    [Fact]
    public void AddedUnrelatedFile_ServesEveryOutputFromCache()
    {
        var compilation = CreateCompilation();
        var next = compilation.AddSyntaxTrees(
            CSharpSyntaxTree.ParseText("namespace MyApp; public sealed class Added { }", path: "Added.cs"));

        AssertAllCached(RunTwice(compilation, next));
    }

    [Fact]
    public void EditedUnrelatedFile_ServesEveryOutputFromCache()
    {
        var compilation = CreateCompilation();
        var unrelated = Tree(compilation, "Unrelated.cs");
        var edited = unrelated.WithChangedText(unrelated.GetText().Replace(
            unrelated.GetText().ToString().IndexOf("1;", StringComparison.Ordinal), 1, "2"));

        AssertAllCached(RunTwice(compilation, compilation.ReplaceSyntaxTree(unrelated, edited)));
    }

    [Fact]
    public void EditedInterface_RegeneratesTheProxy()
    {
        var compilation = CreateCompilation();
        var iface = Tree(compilation, "IOrderService.cs");
        var text = iface.GetText().ToString().Replace("\"orders.created\"", "\"orders.placed\"", StringComparison.Ordinal);
        var edited = iface.WithChangedText(Microsoft.CodeAnalysis.Text.SourceText.From(text));

        var driver = RunTwice(compilation, compilation.ReplaceSyntaxTree(iface, edited));

        var reasons = OutputReasons(driver);
        Assert.Contains(IncrementalStepRunReason.Modified, reasons);
    }

    /// <summary>
    /// Diagnostics are cached as data and rebuilt when reported. They must come back as source
    /// locations in the compilation's own tree: an external-file location, as
    /// <c>Location.Create(filePath, span, lineSpan)</c> makes, is ignored by
    /// <c>#pragma warning disable</c>, so a suppressed warning would start firing again.
    /// </summary>
    [Fact]
    public void CachedDiagnostics_AreReportedAtSourceLocations()
    {
        var compilation = CreateCompilation();
        var driver = RunTwice(compilation, compilation.Clone());
        var diagnostics = driver.GetRunResult().Diagnostics;

        Assert.NotEmpty(diagnostics);
        Assert.All(diagnostics, d =>
        {
            Assert.True(d.Location.IsInSource, d.ToString());
            Assert.Contains(d.Location.SourceTree, compilation.SyntaxTrees);
        });
    }

    /// <summary>
    /// Guards the test itself: with no step names the assertions would pass vacuously.
    /// </summary>
    [Fact]
    public void TrackedStepsAreNamed()
    {
        var compilation = CreateCompilation();
        var result = CreateDriver().RunGenerators(compilation).GetRunResult().Results[0];

        Assert.True(result.TrackedSteps.ContainsKey(InstrumentsStep));
        Assert.True(result.TrackedSteps.ContainsKey(OrphanDiagnosticsStep));
        Assert.NotEmpty(result.TrackedOutputSteps);
    }

    private static GeneratorDriver CreateDriver() =>
        CSharpGeneratorDriver.Create(
            [new InstrumentGenerator().AsSourceGenerator()],
            driverOptions: new GeneratorDriverOptions(
                IncrementalGeneratorOutputKind.None,
                trackIncrementalGeneratorSteps: true));

    private static GeneratorDriver RunTwice(Compilation first, Compilation second)
    {
        var driver = CreateDriver().RunGenerators(first);

        // The first run must actually produce something, or "cached" proves nothing.
        var initial = driver.GetRunResult().Results[0];
        Assert.NotEmpty(initial.GeneratedSources);
        Assert.NotEmpty(initial.Diagnostics);

        return driver.RunGenerators(second);
    }

    private static void AssertAllCached(GeneratorDriver driver)
    {
        var result = driver.GetRunResult().Results[0];

        var named = new[]
        {
            InstrumentsStep,
            OrphanDiagnosticsStep,
        };

        var failures = new List<string>();
        foreach (var name in named)
        {
            foreach (var step in result.TrackedSteps[name])
                CollectUncached(name, step, failures);
        }

        foreach (var (name, steps) in result.TrackedOutputSteps)
        {
            foreach (var step in steps)
                CollectUncached(name, step, failures);
        }

        Assert.True(failures.Count == 0, string.Join(Environment.NewLine, failures));
    }

    private static void CollectUncached(string name, IncrementalGeneratorRunStep step, List<string> failures)
    {
        foreach (var (value, reason) in step.Outputs)
        {
            if (reason is not (IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged))
                failures.Add(string.Create(CultureInfo.InvariantCulture, $"{name}: {reason} {value}"));
        }
    }

    private static IncrementalStepRunReason[] OutputReasons(GeneratorDriver driver) =>
        driver.GetRunResult().Results[0].TrackedOutputSteps
            .SelectMany(kv => kv.Value)
            .SelectMany(step => step.Outputs)
            .Select(output => output.Reason)
            .ToArray();

    private static SyntaxTree Tree(Compilation compilation, string path) =>
        compilation.SyntaxTrees.First(t => string.Equals(t.FilePath, path, StringComparison.Ordinal));

    private static CSharpCompilation CreateCompilation()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        var runtimeRefs = trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => MetadataReference.CreateFromFile(p))
            .ToArray();

        return CSharpCompilation.Create("CachingProbe",
            [
                CSharpSyntaxTree.ParseText(InterfaceSource, path: "IOrderService.cs"),
                CSharpSyntaxTree.ParseText(UnrelatedSource, path: "Unrelated.cs"),
            ],
            runtimeRefs.Concat<MetadataReference>(
            [
                MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location),
            ]),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));
    }
}
