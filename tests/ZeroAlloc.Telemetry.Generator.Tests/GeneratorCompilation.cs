using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Runs the generator over a source and compiles the result, the way a consumer builds: nullable
/// enabled, the generated files added to the compilation.
/// </summary>
internal static class GeneratorCompilation
{
    /// <summary>The generator's own diagnostics, and the diagnostics of the compilation it produced.</summary>
    public static (ImmutableArray<Diagnostic> Generator, ImmutableArray<Diagnostic> Output) Run(string source)
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
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        return (generatorDiagnostics, output.GetDiagnostics());
    }

    /// <summary>
    /// Runs the generator over <paramref name="sources"/>, each in a file of its own, and returns
    /// what it produced together with the compilation that includes it.
    /// </summary>
    public static GeneratorOutput RunAll(params string[] sources)
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        var references = trustedPlatformAssemblies
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location));

        var parseOptions = new CSharpParseOptions(documentationMode: DocumentationMode.None);
        var compilation = CSharpCompilation.Create("CompileProbeAssembly",
            sources.Select((s, i) => CSharpSyntaxTree.ParseText(s, parseOptions, path: $"source{i}.cs")),
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable));

        var driver = CSharpGeneratorDriver
            .Create(new InstrumentGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out _);
        var result = driver.GetRunResult().Results[0];

        var all = output.GetDiagnostics();
        return new GeneratorOutput(
            result.Diagnostics,
            result.GeneratedSources.Select(s => s.HintName).OrderBy(h => h, StringComparer.Ordinal).ToArray(),
            all.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()).ToArray(),
            all.Where(d => d.Severity == DiagnosticSeverity.Warning).Select(d => d.ToString()).ToArray(),
            output);
    }

    /// <summary>
    /// The compiler errors in the output, excluding the generator's own diagnostics. When a
    /// generator error is meant to be the only error, this must be empty.
    /// </summary>
    public static string[] OutputErrors(string source)
    {
        var (_, output) = Run(source);
        return output
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .Select(d => d.ToString())
            .ToArray();
    }
}
