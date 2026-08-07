using System;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.Telemetry;

namespace ZeroAlloc.Telemetry.Generator.Tests;

public class DiagnosticTests
{
    [Fact]
    public void ZTEL001_InstrumentOnClass_ProducesError()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            [Instrument("MyApp")]
            public class OrderService { }
            """);

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZTEL001", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZTEL001_InstrumentOnStruct_ProducesError()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            [Instrument("MyApp")]
            public struct OrderServiceStruct { }
            """);

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZTEL001", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZTEL001_InstrumentOnInterface_ProducesNoError()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            [Instrument("MyApp")]
            public interface IOrderService { }
            """);

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZTEL001", StringComparison.Ordinal));
    }

    [Fact]
    public void ZTEL002_EmptyActivitySource_ProducesError()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            [Instrument("")]
            public interface IOrderService { }
            """);

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZTEL002", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZTEL002_WhitespaceActivitySource_ProducesError()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            [Instrument("   ")]
            public interface IOrderService { }
            """);

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZTEL002", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZTEL002_NonEmptyActivitySource_ProducesNoError()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            [Instrument("MyApp")]
            public interface IOrderService { }
            """);

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZTEL002", StringComparison.Ordinal));
    }

    [Fact]
    public void ZTEL003_TraceOnMethodWithoutInstrumentContainer_ProducesWarning()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;
            public class OrphanService
            {
                [Trace("oops")]
                public ValueTask DoAsync(CancellationToken ct) => default;
            }
            """);

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZTEL003", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ZTEL003_TraceInsideInstrumentedInterface_ProducesNoWarning()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;
            [Instrument("MyApp")]
            public interface IProperService
            {
                [Trace("proper.go")]
                ValueTask GoAsync(CancellationToken ct);
            }
            """);

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZTEL003", StringComparison.Ordinal));
    }

    [Fact]
    public void ZTEL004_TraceTagWithoutTrace_ProducesWarning()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IOrderService
            {
                [Count("orders.created")]
                Task<string> CreateAsync([TraceTag("order.id")] string id, CancellationToken ct);
            }
            """);

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZTEL004", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ZTEL004_TraceTagFromResultWithoutTrace_ProducesWarning()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IOrderService
            {
                [TraceTagFromResult("order.count", "Length")]
                Task<string> CreateAsync(CancellationToken ct);
            }
            """);

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZTEL004", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ZTEL004_TagsWithTrace_ProduceNoWarning()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IOrderService
            {
                [Trace("order.create")]
                [TraceTagFromResult("order.length", "Length")]
                Task<string> CreateAsync([TraceTag("order.id")] string id, CancellationToken ct);
            }
            """);

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZTEL004", StringComparison.Ordinal));
    }

    [Fact]
    public void ZTEL005_ResultTagOnVoidReturningMethod_ProducesWarning()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IOrderService
            {
                [Trace("order.create")]
                [TraceTagFromResult("order.count", "Count")]
                Task CreateAsync(CancellationToken ct);
            }
            """);

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZTEL005", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ZTEL005_ResultTagOnValueReturningMethod_ProducesNoWarning()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IOrderService
            {
                [Trace("order.create")]
                [TraceTagFromResult("order.length", "Length")]
                Task<string> CreateAsync(CancellationToken ct);
            }
            """);

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZTEL005", StringComparison.Ordinal));
    }

    [Fact]
    public void ZTEL004_TraceTagConstantWithoutTrace_ProducesWarning()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IOrderService
            {
                [Count("orders.created")]
                [TraceTagConstant("order.kind", "standard")]
                Task<string> CreateAsync(CancellationToken ct);
            }
            """);

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZTEL004", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Warning);
    }

    private static Diagnostic[] RunAndCollectDiagnostics(string source)
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        var runtimeRefs = trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => MetadataReference.CreateFromFile(p))
            .ToArray();

        var compilation = CSharpCompilation.Create("TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            runtimeRefs.Concat<MetadataReference>(
            [
                MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location),
            ]),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(compilation);
        return driver.GetRunResult().Diagnostics.ToArray();
    }
}
