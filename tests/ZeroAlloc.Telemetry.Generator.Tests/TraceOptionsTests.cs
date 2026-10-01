using System.Globalization;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// The <c>[Trace]</c> additions for the OpenTelemetry GenAI span conventions (#170): span kind,
/// error status from the result, parameter tokens in the name, and tags at creation.
/// </summary>
public class TraceOptionsTests
{
    [Fact]
    public void Kind_IsPassedToStartActivity_AndTheDefaultEmitsWhatItDidBefore()
    {
        var run = GeneratorCompilation.RunAll("""
            using System.Diagnostics;
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [Trace("client", Kind = ActivityKind.Client)] void Client();
                [Trace("internal", Kind = ActivityKind.Internal)] void Internal();
                [Trace("plain")] void Plain();
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        var proxy = Proxy(run, "IOps");
        proxy.Should().Contain("_activitySource.StartActivity(\"client\", ActivityKind.Client);");
        proxy.Should().Contain("_activitySource.StartActivity(\"internal\");");
        proxy.Should().Contain("_activitySource.StartActivity(\"plain\");");
    }

    [Fact]
    public void ErrorWhen_SetsTheErrorStatusFromTheResult_WithItsDescription()
    {
        var run = GeneratorCompilation.RunAll("""
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            public sealed class Error { public string Message { get; init; } = ""; public int Code { get; init; } }
            public sealed class Result { public bool IsFailure { get; init; } public Error? Error { get; init; } }
            public readonly struct StructResult { public bool IsFailure { get; init; } public int Code { get; init; } }
            [Instrument("a")]
            public interface IOps
            {
                [Trace("class", ErrorWhen = "IsFailure", ErrorDescription = "Error.Message")]
                Task<Result> ClassAsync();

                [Trace("struct", ErrorWhen = "IsFailure", ErrorDescription = "Code")]
                StructResult Struct();

                [Trace("bare", ErrorWhen = "IsFailure")]
                ValueTask<Result?> BareAsync();
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        var proxy = Proxy(run, "IOps");
        proxy.Should().Contain("if (_activity is not null && _tagged?.IsFailure == true)\n                _activity.SetStatus(ActivityStatusCode.Error, _tagged?.Error?.Message);");
        proxy.Should().Contain("if (_activity is not null && _result.IsFailure)\n                _activity.SetStatus(ActivityStatusCode.Error, _result.Code.ToString());");
        proxy.Should().Contain("_activity.SetStatus(ActivityStatusCode.Error);");
    }

    [Fact]
    public void ErrorWhen_ThatDoesNotResolve_OrIsNotABool_OrHasNoResult_IsReported()
    {
        const string source = """
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            public sealed class Result { public bool IsFailure { get; init; } public int Code { get; init; } }
            [Instrument("a")]
            public interface IOps
            {
                [Trace("missing", ErrorWhen = "IsFialure")] Result Missing();
                [Trace("notbool", ErrorWhen = "Code")] Result NotBool();
                [Trace("void", ErrorWhen = "IsFailure")] Task Void();
                [Trace("description", ErrorWhen = "IsFailure", ErrorDescription = "Messge")] Result Description();
            }
            """;
        var run = GeneratorCompilation.RunAll(source);

        var messages = run.GeneratorDiagnostics.Select(d => d.Id + " " + d.GetMessage(CultureInfo.InvariantCulture)).ToArray();
        messages.Should().HaveCount(4);
        messages.Should().Contain(m => m.StartsWith("ZTEL007 'IsFialure' in the path 'IsFialure'", StringComparison.Ordinal));
        messages.Should().Contain("ZTEL008 ErrorWhen = 'Code' resolves to 'int'. A guard must name a member of type bool or bool? on the awaited return value, such as IsSuccess.");
        messages.Should().Contain("ZTEL005 [Trace(ErrorWhen = \"IsFailure\")] on 'IOps.Void' records nothing — the method returns void, Task or ValueTask, so there is no result to read. Remove it or return a value.");
        messages.Should().Contain(m => m.StartsWith("ZTEL007 'Messge' in the path 'Messge'", StringComparison.Ordinal));
        run.GeneratorDiagnostics.Where(d => string.Equals(d.Id, "ZTEL007", StringComparison.Ordinal)).Select(d => Text(d)).Should().BeEquivalentTo("\"IsFialure\"", "\"Messge\"");
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void ParameterTokens_StartUnderTheConstantPart_AndSetTheDisplayNameOnlyOnASampledSpan()
    {
        var run = GeneratorCompilation.RunAll("""
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            public sealed class Request { public string? Model { get; init; } public int TopK { get; init; } }
            [Instrument("a")]
            public interface IOps
            {
                [Trace("{operation} {model}")]
                Task<int> ChatAsync(string operation, string? model, CancellationToken ct);

                [Trace("search {request.Model} k={request.TopK} {type}")]
                int Search(Request request);

                [Trace("score {value}")]
                double Score(double value);
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        var proxy = Proxy(run, "IOps");

        proxy.Should().Contain("_activitySource.StartActivity(\"ChatAsync\");");
        proxy.Should().Contain("_activity.DisplayName = string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $\"{operation}{\" \"}{model}\");");

        proxy.Should().Contain("_spanName_Search_1 = \"search k= \" + _implName;");
        proxy.Should().Contain("var _nameArg0 = request;");
        proxy.Should().Contain("_activity.DisplayName = string.Create(global::System.Globalization.CultureInfo.InvariantCulture, $\"{\"search \"}{_nameArg0?.Model}{\" k=\"}{_nameArg1?.TopK}{\" \"}{_inner.GetType().Name}\");");

        proxy.Should().Contain("_activitySource.StartActivity(\"score\");");
        proxy.Should().Contain("if (_activity is not null)");
    }

    [Fact]
    public void ParameterToken_WhosePathDoesNotResolve_ReportsZtel022_AndIsLeftOut()
    {
        var run = GeneratorCompilation.RunAll("""
            using ZeroAlloc.Telemetry;
            public sealed class Request { public string? Model { get; init; } }
            [Instrument("a")]
            public interface IOps { [Trace("chat {request.Modle}")] void Chat(Request request); }
            """);

        var d = run.GeneratorDiagnostics.Should().ContainSingle().Subject;
        d.Id.Should().Be("ZTEL022");
        d.Severity.Should().Be(DiagnosticSeverity.Warning);
        d.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "'Modle' in the token '{request.Modle}' of the [Trace] name on 'IOps.Chat' is not a readable instance property or field of 'Request', so the token is left out of the span name");
        run.Errors.Should().BeEmpty();
        Proxy(run, "IOps").Should().Contain("StartActivity(\"chat\")").And.Contain("$\"{\"chat \"}\"");
    }

    [Fact]
    public void TokenThatNamesNoParameter_ReportsZtel006_AndIsKeptVerbatim()
    {
        var run = GeneratorCompilation.RunAll("""
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps { [Trace("chat {modle}")] void Chat(string model); }
            """);

        var d = run.GeneratorDiagnostics.Should().ContainSingle().Subject;
        d.Id.Should().Be("ZTEL006");
        d.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "'{modle}' in the [Trace] name on 'IOps.Chat' is not a recognised token and is emitted verbatim, so the span name will contain a literal brace. The supported tokens are {type}, which substitutes the wrapped implementation's type name, and {parameter} or {parameter.Member} for a parameter of the method.");
        Proxy(run, "IOps").Should().Contain("StartActivity(\"chat {modle}\")").And.NotContain("DisplayName");
    }

    [Fact]
    public void TagsAtStart_PassesTheConstantAndParameterTagsToStartActivity()
    {
        var run = GeneratorCompilation.RunAll("""
            using System.Collections.Generic;
            using System.Diagnostics;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            public sealed class Request { public string? Model { get; init; } }
            [Instrument("a")]
            public interface IOps
            {
                [Trace("{operation} {request.Model}", Kind = ActivityKind.Client, TagsAtStart = true)]
                [TraceTagConstant("gen_ai.provider.name", "openai")]
                ValueTask<int> ChatAsync(
                    [TraceTag("gen_ai.operation.name")] string operation,
                    [TraceTag("gen_ai.request.model", "Model")] Request request,
                    [TraceTag("gen_ai.request.top_k")] int topK);

                [Trace("set", TagsAtStart = true)]
                TSet Copy<TSet>([TraceTag("set.count", "Count")] TSet set) where TSet : ISet<TSet>;

                [Trace("none", TagsAtStart = true)]
                void None();
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        var proxy = Proxy(run, "IOps");
        proxy.Should().Contain("""
                    using var _activity = _activitySource.HasListeners()
                        ? _activitySource.StartActivity("ChatAsync", ActivityKind.Client, default(ActivityContext), _startTags_ChatAsync_0(operation, request, topK))
                        : null;
            """.Replace("\r", "", StringComparison.Ordinal));
        proxy.Should().Contain("""
                private static TagList _startTags_ChatAsync_0(string operation, global::Request request, int topK)
                {
                    var _startTags = new TagList();
                    _startTags.Add("gen_ai.provider.name", "openai");
                    if (operation is { } _startTag0)
                        _startTags.Add("gen_ai.operation.name", _startTag0);
                    if (request?.Model is { } _startTag1)
                        _startTags.Add("gen_ai.request.model", _startTag1);
                    _startTags.Add("gen_ai.request.top_k", topK);
                    return _startTags;
                }
            """.Replace("\r", "", StringComparison.Ordinal));
        proxy.Should().NotContain("_activity?.SetTag(\"gen_ai");
        proxy.Should().Contain("private static TagList _startTags_Copy_1<TSet>(TSet set)\n        where TSet : global::System.Collections.Generic.ISet<TSet>");
        proxy.Should().Contain("_activitySource.StartActivity(\"none\");");

        // A member of a type parameter resolves through its constraints, read null-safely
        // because an unconstrained type parameter may be a reference type.
        proxy.Should().Contain("if (set?.Count is { } _startTag0)");
    }

    [Fact]
    public void GenAiChatSpan_Snapshot()
    {
        const string source = """
            using System.Diagnostics;
            using System.Threading;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            public sealed class ChatError { public string Message { get; init; } = ""; }
            public readonly struct ChatResult
            {
                public bool IsFailure { get; init; }
                public ChatError? Error { get; init; }
                public string? ResponseModel { get; init; }
            }
            [Instrument("gen_ai")]
            public interface IChat
            {
                [Trace("{operation} {model}", Kind = ActivityKind.Client, ErrorWhen = "IsFailure", ErrorDescription = "Error.Message", TagsAtStart = true)]
                [TraceTagConstant("gen_ai.provider.name", "openai")]
                [TraceTagFromResult("gen_ai.response.model", "ResponseModel")]
                ValueTask<ChatResult> ChatAsync(
                    [TraceTag("gen_ai.operation.name")] string operation,
                    [TraceTag("gen_ai.request.model")] string model,
                    CancellationToken ct);
            }
            """;

        GeneratorSnapshot.Verify(CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(
            CSharpCompilation.Create("TestAssembly", [CSharpSyntaxTree.ParseText(source)], GeneratorTestReferences.All(),
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))));
    }

    private static string Proxy(GeneratorOutput run, string interfaceName) =>
        run.Output.SyntaxTrees
            .First(t => t.FilePath.EndsWith(interfaceName + ".Instrumented.g.cs", StringComparison.Ordinal))
            .ToString()
            .Replace("\r", "", StringComparison.Ordinal);

    private static string Text(Diagnostic d) =>
        d.Location.SourceTree!.GetText().ToString(d.Location.SourceSpan);
}
