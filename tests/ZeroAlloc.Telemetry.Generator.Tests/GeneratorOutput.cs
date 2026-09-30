using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>What <see cref="GeneratorCompilation.RunAll"/> produced.</summary>
internal sealed record GeneratorOutput(
    ImmutableArray<Diagnostic> GeneratorDiagnostics,
    string[] HintNames,
    string[] Errors,
    string[] Warnings,
    Compilation Output);
