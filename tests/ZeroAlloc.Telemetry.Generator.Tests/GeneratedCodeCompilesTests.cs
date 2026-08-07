using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Compiles the generator's output and asserts it produces no errors.
/// <para>
/// The snapshot tests assert on emitted <em>text</em>, which cannot catch a shape that simply does
/// not compile — and member-path emission has two ways to produce exactly that. Using <c>?.</c>
/// after a non-nullable value type is a compile error, and omitting it where a value can be null
/// is an NRE at runtime. Only the compiler settles the first.
/// </para>
/// </summary>
public class GeneratedCodeCompilesTests
{
    // Deliberately mixes nullable reference, non-nullable struct, and nullable value types
    // along the paths, so every operator decision the resolver makes is exercised.
    private const string ProbeSource = """
            using ZeroAlloc.Telemetry;
            using System.Collections.Generic;
            using System.Threading;
            using System.Threading.Tasks;

            public enum ProbeMode { First = 1, Second = 2 }

            public readonly struct Extent { public int Width { get; } }

            public sealed class Inner { public IReadOnlyList<string>? Items { get; set; } }

            public sealed class Outer
            {
                public Inner? Inner { get; set; }
                public int Total { get; set; }
                public Extent Extent { get; set; }
                public int? Optional { get; set; }
            }

            [Instrument("MyApp.Compile")]
            public interface ICompileProbe
            {
                [Trace("probe.deep")]
                [TraceTagFromResult("deep.count", "Inner.Items.Count")]
                [TraceTagFromResult("total", "Total")]
                [TraceTagFromResult("width", "Extent.Width")]
                [TraceTagFromResult("optional", "Optional.Value")]
                Task<Outer> DeepAsync(CancellationToken ct);

                [Trace("probe.struct")]
                [TraceTagFromResult("struct.width", "Width")]
                ValueTask<Extent> StructAsync(CancellationToken ct);

                // Task<int?> tagged with "Value" — the shape the existing snapshot covers.
                // `?.Value` on a nullable value type unwraps first, so .Value lands on int.
                [Trace("probe.nullable")]
                [TraceTagFromResult("nullable.value", "Value")]
                Task<int?> NullableAsync(CancellationToken ct);

                // Constant tags: every kind must render as a compilable literal, including
                // strings needing escapes and an enum with no single named member.
                // Parameter member paths: the tagged argument is still forwarded to the inner
                // call, so a null test on it must not leak into its null-state (CS8604).
                [Trace("probe.paramPath")]
                Task ParamPathAsync(
                    [TraceTag("p.count", "Count")] IReadOnlyList<string> items,
                    [TraceTag("p.width", "Width")] Extent extent,
                    [TraceTag("p.deep", "Inner.Items.Count")] Outer outer,
                    CancellationToken ct);

                [Trace("probe.constant")]
                [TraceTagConstant("c.string", "a \"quoted\" and C:\\path")]
                [TraceTagConstant("c.bool", false)]
                [TraceTagConstant("c.int", -7)]
                [TraceTagConstant("c.enum", ProbeMode.Second)]
                Task<string> ConstantAsync(CancellationToken ct);
            }
        """;

    [Fact]
    public void GeneratedOutput_Compiles()
    {
        var errors = CompileWithGenerator(ProbeSource);

        Assert.True(
            errors.Length == 0,
            "Generated code did not compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    private static string[] CompileWithGenerator(string source)
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        var runtimeRefs = trustedPlatformAssemblies
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => MetadataReference.CreateFromFile(p))
            .ToArray();

        // Nullable enabled: the annotations on the probe types only carry meaning with it on,
        // and it is how consumers build.
        var parseOptions = new CSharpParseOptions(documentationMode: DocumentationMode.None);
        var compilation = CSharpCompilation.Create("CompileProbeAssembly",
            [CSharpSyntaxTree.ParseText(source, parseOptions)],
            runtimeRefs.Concat<MetadataReference>(
            [
                MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location),
            ]),
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        CSharpGeneratorDriver
            .Create(new InstrumentGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);

        var errors = new List<string>();
        foreach (var d in output.GetDiagnostics())
        {
            if (d.Severity == DiagnosticSeverity.Error)
                errors.Add(d.ToString());
        }

        return errors.ToArray();
    }
}
