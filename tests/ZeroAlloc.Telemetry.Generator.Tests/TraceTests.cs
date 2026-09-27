using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Telemetry.Generator.Tests;

public class TraceTests
{
    [Fact]
    public void GeneratesActivityProxy_ForTraceMethod()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IOrderService
            {
                [Trace("order.create")]
                ValueTask CreateOrderAsync(string orderId, CancellationToken ct);
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    [Fact]
    public void GeneratesCounterIncrement_ForCountMethod()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IOrderService
            {
                [Count("orders.created")]
                ValueTask CreateOrderAsync(string orderId, CancellationToken ct);
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    [Fact]
    public void GeneratesHistogramRecord_ForHistogramMethod()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IOrderService
            {
                [Histogram("order.duration_ms")]
                ValueTask<string> GetOrderAsync(string orderId, CancellationToken ct);
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    [Fact]
    public void GeneratesAllInstruments_WhenAllAttributesCombined()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp.Orders")]
            public interface IOrderService
            {
                [Trace("order.create")]
                [Count("orders.created")]
                [Histogram("order.create_ms")]
                ValueTask<string> CreateOrderAsync(string orderId, CancellationToken ct);
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    /// <summary>
    /// A return type whose name contains "Task" is not a task. Matching the name made the proxy
    /// emit <c>async</c> and <c>await</c> for a synchronous method, which does not compile.
    /// </summary>
    [Fact]
    public void GeneratesSyncProxy_ForReturnTypeNamedLikeTask()
    {
        var source = """
            using ZeroAlloc.Telemetry;

            public sealed class TaskReport { public int Count { get; set; } }

            [Instrument("MyApp")]
            public interface IReportService
            {
                [Count("reports.built")]
                TaskReport Build();
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    /// <summary>
    /// A custom <c>[AsyncMethodBuilder]</c> task-like type, as PooledAwait ships. 1.6.4 matched
    /// return types by name and awaited these; the proxy must still be <c>async</c>, so the span
    /// and the histogram end when the task completes rather than when it is returned, and result
    /// reads use the awaited <c>T</c> taken from <c>GetAwaiter().GetResult()</c>.
    /// </summary>
    [Fact]
    public void GeneratesAsyncProxy_ForCustomTaskLikeReturnType()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Collections.Generic;
            using Pooled;

            [Instrument("MyApp")]
            public interface IPooledService
            {
                [Trace("pooled.list")]
                [Histogram("pooled.list.ms")]
                PooledTask<List<int>> ListAsync();

                [Trace("pooled.count")]
                [TraceTagFromResult("items.count", "Count")]
                [CountFromResult("pooled.items", "Count")]
                PooledTask<List<int>> CountAsync();

                [Trace("pooled.run")]
                [Histogram("pooled.run.ms")]
                PooledTask RunAsync();
            }
            """ + TaskLikeTypes.Declarations;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    private static GeneratorDriver RunGenerator(string source)
    {
        // Collect all runtime refs that the test process has loaded so the compilation
        // can resolve Attribute, ValueTask, CancellationToken, etc.
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
