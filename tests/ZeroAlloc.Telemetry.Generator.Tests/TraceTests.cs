using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using VerifyXunit;

namespace ZeroAlloc.Telemetry.Generator.Tests;

public class TraceTests
{
    [Fact]
    public Task GeneratesActivityProxy_ForTraceMethod()
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

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesCounterIncrement_ForCountMethod()
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

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesHistogramRecord_ForHistogramMethod()
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

        return Verifier.Verify(RunGenerator(source));
    }

    [Fact]
    public Task GeneratesAllInstruments_WhenAllAttributesCombined()
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

        return Verifier.Verify(RunGenerator(source));
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
