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

        Assert.Contains(diagnostics, d => d.Id == "ZTEL001" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZTEL001_InstrumentOnStruct_ProducesError()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            [Instrument("MyApp")]
            public struct OrderServiceStruct { }
            """);

        Assert.Contains(diagnostics, d => d.Id == "ZTEL001" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZTEL001_InstrumentOnInterface_ProducesNoError()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            [Instrument("MyApp")]
            public interface IOrderService { }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "ZTEL001");
    }

    [Fact]
    public void ZTEL002_EmptyActivitySource_ProducesError()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            [Instrument("")]
            public interface IOrderService { }
            """);

        Assert.Contains(diagnostics, d => d.Id == "ZTEL002" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZTEL002_WhitespaceActivitySource_ProducesError()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            [Instrument("   ")]
            public interface IOrderService { }
            """);

        Assert.Contains(diagnostics, d => d.Id == "ZTEL002" && d.Severity == DiagnosticSeverity.Error);
    }

    [Fact]
    public void ZTEL002_NonEmptyActivitySource_ProducesNoError()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            [Instrument("MyApp")]
            public interface IOrderService { }
            """);

        Assert.DoesNotContain(diagnostics, d => d.Id == "ZTEL002");
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

        Assert.Contains(diagnostics, d => d.Id == "ZTEL003" && d.Severity == DiagnosticSeverity.Warning);
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

        Assert.DoesNotContain(diagnostics, d => d.Id == "ZTEL003");
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
