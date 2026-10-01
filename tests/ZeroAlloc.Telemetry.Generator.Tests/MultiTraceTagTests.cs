using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// A parameter may carry several <c>[TraceTag]</c> (#181), such as <c>server.address</c> and
/// <c>server.port</c> from one <c>Uri</c>. Before, a second one was CS0579.
/// </summary>
public class MultiTraceTagTests
{
    private const string JevSource = """
        using System;
        using System.Collections.Generic;
        using System.Threading;
        using System.Threading.Tasks;
        using ZeroAlloc.Telemetry;
        namespace Jev
        {
            public sealed class AskRequest
            {
                public string Model { get; init; } = "";
                public IReadOnlyList<string> Questions { get; init; } = [];
            }
            [Instrument("ZeroAlloc.Jev")]
            internal interface IJevOperations
            {
                [Trace("ask"{{START}})]
                ValueTask<int> AskAsync(
                    [TraceTag("server.address", "Host")] [TraceTag("server.port", "Port")] Uri endpoint,
                    [TraceTag("gen_ai.request.model", "Model")] [TraceTag("jev.question.count", "Questions.Count")] AskRequest request,
                    CancellationToken ct);
            }
        }
        """;

    [Fact]
    public void SeveralTagsOnOneParameter_AreSetAfterTheSpanStarts()
    {
        var run = GeneratorCompilation.RunAll(JevSource.Replace("{{START}}", "", StringComparison.Ordinal));

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        var proxy = Proxy(run);
        proxy.Should().Contain("""
                    var _tag_endpoint = endpoint;
                    _activity?.SetTag("server.address", _tag_endpoint?.Host);
                    _activity?.SetTag("server.port", _tag_endpoint?.Port);
                    var _tag_request = request;
                    _activity?.SetTag("gen_ai.request.model", _tag_request?.Model);
                    _activity?.SetTag("jev.question.count", _tag_request?.Questions?.Count);
            """.Replace("\r", "", StringComparison.Ordinal));
    }

    [Fact]
    public void SeveralTagsOnOneParameter_ArePassedToTheSampler_WithTagsAtStart()
    {
        var run = GeneratorCompilation.RunAll(JevSource.Replace("{{START}}", ", TagsAtStart = true", StringComparison.Ordinal));

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        var proxy = Proxy(run);
        proxy.Should().Contain("""
                    if (endpoint?.Host is { } _startTag0)
                        _startTags.Add("server.address", _startTag0);
                    if (endpoint?.Port is { } _startTag1)
                        _startTags.Add("server.port", _startTag1);
                    if (request?.Model is { } _startTag2)
                        _startTags.Add("gen_ai.request.model", _startTag2);
                    if (request?.Questions?.Count is { } _startTag3)
                        _startTags.Add("jev.question.count", _startTag3);
            """.Replace("\r", "", StringComparison.Ordinal));
        // The exception path's error.type is the only tag set after the span starts (#184).
        proxy.Replace("_activity?.SetTag(\"error.type\"", "", StringComparison.Ordinal)
            .Should().NotContain("_activity?.SetTag(");
    }

    [Fact]
    public void JevShape_Snapshot()
    {
        var source = JevSource.Replace("{{START}}", ", TagsAtStart = true", StringComparison.Ordinal);
        GeneratorSnapshot.Verify(CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(
            CSharpCompilation.Create("TestAssembly", [CSharpSyntaxTree.ParseText(source)], GeneratorTestReferences.All(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))));
    }

    [Fact]
    public void TheSameTagNameTwice_ReportsZtel026_AtTheLaterOne_WhichIsNotSet()
    {
        const string source = """
            using System;
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [Trace("run")]
                [TraceTagConstant("server.address", "fixed")]
                void Run(
                    [TraceTag("server.address", "Host")] [TraceTag("server.port", "Port")] Uri endpoint,
                    [TraceTag("server.port")] int port);
            }
            """;
        var run = GeneratorCompilation.RunAll(source);

        run.GeneratorDiagnostics.Select(d => d.Id).Should().Equal("ZTEL026", "ZTEL026");
        var first = run.GeneratorDiagnostics[0];
        first.Severity.Should().Be(DiagnosticSeverity.Warning);
        first.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "[TraceTag] adds the tag 'server.address' to the span of 'IOps.Run', which another tag already sets. A span keeps one value per tag name, so remove or rename one of them; this one is not set.");
        first.Location.SourceTree!.GetText().ToString(first.Location.SourceSpan).Should().Be("\"server.address\"");
        first.Location.SourceSpan.Start.Should().Be(source.IndexOf("\"server.address\", \"Host\"", StringComparison.Ordinal));
        run.Errors.Should().BeEmpty();

        var proxy = Proxy(run, "IOps");
        proxy.Should().Contain("_activity?.SetTag(\"server.address\", \"fixed\");");
        proxy.Should().Contain("_activity?.SetTag(\"server.port\", _tag_endpoint?.Port);");
        proxy.Should().NotContain("Host").And.NotContain("SetTag(\"server.port\", port)");
    }

    [Fact]
    public void APathThatDoesNotResolve_ReportsZtel010_AndKeepsTheOtherTag()
    {
        const string source = """
            using System;
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [Trace("run")] void Run([TraceTag("server.address", "Hots")] [TraceTag("server.port", "Port")] Uri endpoint);
            }
            """;
        var run = GeneratorCompilation.RunAll(source);

        run.GeneratorDiagnostics.Should().ContainSingle().Which.Id.Should().Be("ZTEL010");
        run.Errors.Should().BeEmpty();
        Proxy(run, "IOps").Should().Contain("_activity?.SetTag(\"server.port\", _tag_endpoint?.Port);").And.NotContain("server.address");
    }

    [Fact]
    public void SeveralTagsWithoutTrace_ReportZtel004_Once()
    {
        var run = GeneratorCompilation.RunAll("""
            using System;
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps { void Run([TraceTag("server.address", "Host")] [TraceTag("server.port", "Port")] Uri endpoint); }
            """);

        run.GeneratorDiagnostics.Should().ContainSingle().Which.Id.Should().Be("ZTEL004");
        run.Errors.Should().BeEmpty();
    }

    private static string Proxy(GeneratorOutput run, string interfaceName = "IJevOperations") =>
        run.Output.SyntaxTrees
            .First(t => t.FilePath.EndsWith(interfaceName + ".Instrumented.g.cs", StringComparison.Ordinal))
            .ToString()
            .Replace("\r", "", StringComparison.Ordinal);
}
