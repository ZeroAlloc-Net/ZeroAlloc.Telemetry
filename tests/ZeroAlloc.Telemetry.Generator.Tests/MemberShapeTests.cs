using System.Globalization;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Every member shape an interface can declare either compiles in the proxy or is reported
/// (#173): parameter modifiers, ref structs on awaitable methods, properties, indexers, events,
/// inherited and static members.
/// </summary>
public class MemberShapeTests
{
    [Fact]
    public void SynchronousMethods_WithEveryParameterModifier_AreInstrumented()
    {
        var run = GeneratorCompilation.RunAll("""
            using System;
            using System.Diagnostics.CodeAnalysis;
            using ZeroAlloc.Telemetry;
            namespace App
            {
                [Instrument("a")]
                public interface IOps
                {
                    [Trace("ref")] [Count("ref.calls")] void Ref(ref int value);
                    [Trace("out")] [Histogram("out.duration")] bool TryGet(string key, [NotNullWhen(true)] out string? value);
                    [Trace("in")] int In(in Guid id, [TraceTag("id")] in Guid tagged);
                    [Trace("ref-readonly")] int RefReadonly(ref readonly int value);
                    [Trace("scoped")] int Scoped(scoped ReadOnlySpan<char> text, [TraceTag("length", "Length")] ReadOnlySpan<char> other);
                    [Trace("params")] int Sum(params int[] values);
                    [Trace("params-span")] int SumSpan(params ReadOnlySpan<int> values);
                    [Histogram("metric.out")] int Parse(string text, [MetricTag("consumed")] out int consumed);
                    void Plain(ref int value, out int result);
                }
                internal sealed class Ops : IOps
                {
                    public void Ref(ref int value) => value++;
                    public bool TryGet(string key, [NotNullWhen(true)] out string? value) { value = key; return true; }
                    public int In(in Guid id, in Guid tagged) => 0;
                    public int RefReadonly(ref readonly int value) => value;
                    public int Scoped(scoped ReadOnlySpan<char> text, ReadOnlySpan<char> other) => text.Length;
                    public int Sum(params int[] values) => values.Length;
                    public int SumSpan(params ReadOnlySpan<int> values) => values.Length;
                    public int Parse(string text, out int consumed) { consumed = text.Length; return 1; }
                    public void Plain(ref int value, out int result) => result = value;
                }
                internal static class Calls
                {
                    public static int Use()
                    {
                        IOps ops = new OpsInstrumented(new Ops());
                        var value = 1;
                        ops.Ref(ref value);
                        if (ops.TryGet("k", out var found)) value += found.Length;
                        return value + ops.Sum(1, 2) + ops.SumSpan(1, 2) + ops.Parse("x", out var consumed) + consumed;
                    }
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        var proxy = Proxy(run, "IOps");
        proxy.Should().Contain("public bool TryGet(string key, [global::System.Diagnostics.CodeAnalysis.NotNullWhenAttribute(true)] out string? value)");
        proxy.Should().Contain("var _result = _inner.TryGet(key, out value);");
        proxy.Should().Contain("public int Scoped(scoped global::System.ReadOnlySpan<char> text, global::System.ReadOnlySpan<char> other)");
        proxy.Should().Contain("_activity?.SetTag(\"length\", other.Length);");
        proxy.Should().Contain("public int SumSpan(params global::System.ReadOnlySpan<int> values)");

        // An out parameter has its value after the call, but not on the throw path.
        var parse = proxy[proxy.IndexOf("public int Parse(", StringComparison.Ordinal)..];
        parse.Should().Contain("_metricTags0.Add(\"consumed\", consumed);");
        parse[parse.IndexOf("catch", StringComparison.Ordinal)..].Should().NotContain("consumed");
    }

    [Fact]
    public void AwaitableMethods_WithParametersAnAsyncMethodCannotTake_AreInstrumented()
    {
        var run = GeneratorCompilation.RunAll("""
            using System;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            namespace App
            {
                public sealed class Reply { public bool IsFailure { get; init; } public int Size { get; init; } }
                [Instrument("a")]
                public interface IOps
                {
                    [Trace("write {name}", ErrorWhen = "IsFailure", TagsAtStart = true)]
                    [Histogram("write.duration")]
                    [CountFromResult("write.size", "Size")]
                    ValueTask<Reply> WriteAsync([TraceTag("name")] [MetricTag("name")] string name, ReadOnlySpan<byte> data, [TraceTag("seed")] ref int seed);

                    [Count("read.calls")]
                    Task<int> ReadAsync(Span<byte> buffer, out int read);

                    ValueTask StepAsync(in Guid id);

                    [Trace("generic")]
                    ValueTask<T> EchoAsync<T>(ref T value) where T : notnull;
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        var proxy = Proxy(run, "IOps");
        proxy.Should().Contain("_pending = _inner.WriteAsync(name, data, ref seed);");
        proxy.Should().Contain("return _core_WriteAsync_0(_pending, _activity, _sw, name, seed);");
        proxy.Should().Contain("var _result = await _pending.ConfigureAwait(false);");
        proxy.Should().Contain("_activity?.Dispose();");
        proxy.Should().Contain("return _inner.StepAsync(in id);");
    }

    [Fact]
    public void PropertiesIndexersAndEvents_AreForwarded()
    {
        var run = GeneratorCompilation.RunAll("""
            using System;
            using ZeroAlloc.Telemetry;
            namespace App
            {
                [Instrument("a")]
                public interface IOps
                {
                    int Count { get; set; }
                    string? Name { get; }
                    int Written { set; }
                    int Init { get; init; }
                    ref int Slot { get; }
                    ref readonly int Frozen { get; }
                    string this[int index] { get; set; }
                    int this[in Guid id, string key] { get; }
                    event EventHandler? Changed;
                    event Action<int> Counted;
                    ref int Pick(int index);
                    [Trace("run")] void Run();
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        run.Warnings.Should().BeEmpty();
        var proxy = Proxy(run, "IOps");
        proxy.Should().Contain("get => _inner.Count;");
        proxy.Should().Contain("set => _inner.Count = value;");
        proxy.Should().Contain("public ref int Slot");
        proxy.Should().Contain("get => ref _inner.Slot;");
        proxy.Should().Contain("public string this[int index]");
        proxy.Should().Contain("get => _inner[in id, key];");
        proxy.Should().Contain("add => _inner.Changed += value;");
        proxy.Should().Contain("=> ref _inner.Pick(index);");
        proxy.Should().Contain("init => throw new NotSupportedException(");
    }

    [Fact]
    public void InheritedMembers_AreImplementedOnce_AndStaticMembersAreLeftAlone()
    {
        var run = GeneratorCompilation.RunAll("""
            using ZeroAlloc.Telemetry;
            namespace App
            {
                public interface IReader { [Trace("read")] int Read(); int Size { get; } }
                public interface IWriter { void Write(int value); int Read(); }
                public interface IGeneric<T> { T Get(); }
                [Instrument("a")]
                public interface IStore : IReader, IWriter, IGeneric<string>
                {
                    void Flush();
                    static int Shared() => 1;
                    static int SharedValue { get; set; }
                    void Default() { }
                }
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        var proxy = Proxy(run, "IStore");
        proxy.Should().Contain("public string Get()");
        proxy.Should().Contain("_activitySource.StartActivity(\"read\")");
        CountOccurrences(proxy, "public int Read()").Should().Be(1);
        proxy.Should().Contain("((global::App.IReader)_inner).Read()");
        proxy.Should().NotContain("Shared");
    }

    [Fact]
    public void InstrumentationOnAnAccessorOrARefReturningMethod_ReportsZtel023()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                int Count { [Trace("count")] get; }
                [Count("pick")] ref int Pick();
            }
            """;
        var run = GeneratorCompilation.RunAll(source);

        var messages = run.GeneratorDiagnostics.Select(d => d.Id + " " + d.GetMessage(CultureInfo.InvariantCulture)).ToArray();
        messages.Should().BeEquivalentTo(
            "ZTEL023 [Trace] on 'IOps.Count' records nothing — instrumentation is not supported on a property, indexer or event accessor. The member is forwarded without instrumentation.",
            "ZTEL023 [Count] on 'IOps.Pick' records nothing — a method that returns by reference cannot be instrumented. The member is forwarded without instrumentation.");
        run.GeneratorDiagnostics.Should().OnlyContain(d => d.Severity == DiagnosticSeverity.Warning);
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void TagsAndTokensOnUnreadableParameters_ReportZtel024()
    {
        const string source = """
            using System;
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [Trace("get {result}")] bool TryGet([TraceTag("result")] out int result);
                [Trace("span")] void Span([TraceTag("text")] ReadOnlySpan<char> text, [TraceTag("first", "Span")] Holder holder);
                [Count("write")] Task WriteAsync([MetricTag("data")] ReadOnlyMemory<byte> ok, [MetricTag("length", "Length")] ReadOnlySpan<byte> data);
            }
            public ref struct Inner { }
            public readonly ref struct Holder { public ReadOnlySpan<char> Span => default; }
            """;
        var run = GeneratorCompilation.RunAll(source);

        var messages = run.GeneratorDiagnostics.Select(d => d.Id + " " + d.GetMessage(CultureInfo.InvariantCulture)).ToArray();
        messages.Should().BeEquivalentTo(
            "ZTEL024 [TraceTag] on parameter 'result' of 'IOps.TryGet' records nothing — an out parameter has no value until the call returns",
            "ZTEL024 [Trace] on parameter 'result' of 'IOps.TryGet' records nothing — an out parameter has no value until the call returns",
            "ZTEL024 [TraceTag] on parameter 'text' of 'IOps.Span' records nothing — a ref struct cannot be boxed into a tag value",
            "ZTEL024 [TraceTag] on parameter 'holder' of 'IOps.Span' records nothing — a ref struct cannot be boxed into a tag value",
            "ZTEL024 [MetricTag] on parameter 'data' of 'IOps.WriteAsync' records nothing — a ref struct argument cannot be carried past the await of an async method");
        run.Errors.Should().BeEmpty();
    }

    [Fact]
    public void StaticAbstractMember_ReportsZtel025_AndGetsNoProxy()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IParser<TSelf> where TSelf : IParser<TSelf>
            {
                static abstract TSelf Parse(string text);
                void Run();
            }
            [Instrument("b")] public interface IKept { void Run(); }
            """;
        var run = GeneratorCompilation.RunAll(source);

        var d = run.GeneratorDiagnostics.Should().ContainSingle().Subject;
        d.Id.Should().Be("ZTEL025");
        d.Severity.Should().Be(DiagnosticSeverity.Error);
        d.GetMessage(CultureInfo.InvariantCulture).Should().Be(
            "Instrumented interface 'IParser<TSelf>' gets no proxy because its member 'IParser<TSelf>.Parse(string)' is static abstract, and a proxy cannot forward a static member to the instance it wraps");
        run.HintNames.Should().Equal("IKept.Instrumented.g.cs");
        run.Errors.Should().BeEmpty();
    }

    private static string Proxy(GeneratorOutput run, string interfaceName) =>
        run.Output.SyntaxTrees
            .First(t => t.FilePath.EndsWith(interfaceName + ".Instrumented.g.cs", StringComparison.Ordinal))
            .ToString();

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
