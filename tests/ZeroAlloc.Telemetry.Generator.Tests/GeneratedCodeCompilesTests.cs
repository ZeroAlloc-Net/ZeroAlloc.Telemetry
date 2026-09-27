using Microsoft.CodeAnalysis;

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

            public sealed class ProbeResult
            {
                public bool IsSuccess { get; }
                public bool? MaybeOk { get; }
                public Inner? Value { get; }
            }

            public readonly struct ProbeOutcome
            {
                public bool Ok { get; }
                public int Count { get; }
            }

            public readonly struct Extent { public int Width { get; } }

            public sealed class Inner { public IReadOnlyList<string>? Items { get; set; } }

            public sealed class TaskReport { public int Count { get; set; } }

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

                // "Value" on a nullable value type is dropped because ?. already unwraps it. The
                // next segment must then stay null-safe, and a guard ending there compares to true.
                [Trace("probe.nullableStruct")]
                [TraceTagFromResult("ns.width", "Value.Width")]
                Task<Extent?> NullableStructAsync(CancellationToken ct);

                [Trace("probe.nullableBoolGuard")]
                [TraceTagFromResult("nb.flag", When = "Value")]
                Task<bool?> NullableBoolGuardAsync(CancellationToken ct);

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

                // When guards: nullable reference root, nullable bool guard, and a
                // non-nullable struct root that must not get a null-tolerant comparison.
                [Trace("probe.guard")]
                [TraceTagFromResult("g.count", "Value.Items.Count", When = "IsSuccess")]
                [TraceTagFromResult("g.maybe", "Value.Items.Count", When = "MaybeOk")]
                Task<ProbeResult> GuardAsync(CancellationToken ct);

                [Trace("probe.guardStruct")]
                [TraceTagFromResult("g.structCount", "Count", When = "Ok")]
                ValueTask<ProbeOutcome> GuardStructAsync(CancellationToken ct);

                [Trace("probe.constant")]
                [TraceTagConstant("c.string", "a \"quoted\" and C:\\path")]
                [TraceTagConstant("c.bool", false)]
                [TraceTagConstant("c.int", -7)]
                [TraceTagConstant("c.enum", ProbeMode.Second)]
                Task<string> ConstantAsync(CancellationToken ct);

                // [Count] and [Histogram] without [Trace]: nothing reads the exception, so the
                // catch must not declare it. CS0168 breaks TreatWarningsAsErrors consumers.
                [Count("probe.counted")]
                Task<int> CountOnlyAsync(CancellationToken ct);

                [Histogram("probe.timed")]
                Task TimedOnlyAsync(CancellationToken ct);

                // Synchronous, with a return type whose name merely contains "Task".
                [Count("probe.sync")]
                TaskReport Report();
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

    // The quoted names cover every string the writer emits as a literal: the activity source,
    // the span name, a constant tag, a result tag and a parameter tag.
    private const string FieldNameProbeSource = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            [Instrument("quoted \"source\"")]
            public interface IFieldNameProbe
            {
                [Count("x")]
                Task<int> CountedAsync(CancellationToken ct);

                [Histogram("x")]
                Task<int> TimedAsync(CancellationToken ct);

                [Count("a.b")]
                [Histogram("a_b")]
                Task<int> DottedAsync(CancellationToken ct);

                [Count("http/requests total")]
                Task<int> SlashAsync(CancellationToken ct);

                [Count("meter")]
                [Histogram("result")]
                Task<int> ReservedAsync(CancellationToken ct);

                [Trace("span \"name\"")]
                [Count("say \"hi\"")]
                [TraceTagConstant("tag \"constant\"", 1)]
                [TraceTagFromResult("tag \"result\"")]
                Task<int> QuotedAsync([TraceTag("tag \"arg\"")] string arg, CancellationToken ct);
            }
        """;

    [Fact]
    public void FieldNameProbe_Compiles()
    {
        var errors = CompileWithGenerator(FieldNameProbeSource);

        Assert.True(
            errors.Length == 0,
            "Generated code did not compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    // Every result-driven shape with nullable enabled, including a Result whose Value throws when
    // unset, so an unguarded read would be a runtime defect as well as a type error.
    private const string ResultMetricsProbeSource = """
            using ZeroAlloc.Telemetry;
            using System;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class Usage
            {
                public int Input { get; set; }
                public long? Output { get; set; }
                public byte Small { get; set; }
                public sbyte Signed { get; set; }
                public short Short { get; set; }
                public ushort UShort { get; set; }
                public uint UInt { get; set; }
                public ulong Big { get; set; }
                public float Ratio { get; set; }
                public double Score { get; set; }
                public decimal Cost { get; set; }
                public decimal? MaybeCost { get; set; }
            }

            public sealed class ClassResult
            {
                public bool IsSuccess { get; }
                public bool? MaybeOk { get; }
                public Usage? Value { get; }
            }

            public readonly struct StructResult
            {
                private readonly Usage? _value;
                public bool IsSuccess { get; }
                public Usage Value => IsSuccess ? _value! : throw new InvalidOperationException();
            }

            public readonly struct Extent { public int Width { get; } }

            [Instrument("MyApp.ResultMetrics")]
            public interface IResultMetricsProbe
            {
                [Trace("probe.class")]
                [TraceTagFromResult("t.input", "Value.Input", When = "IsSuccess")]
                [CountFromResult("read0", "Value.Output")]
                [CountFromResult("c.input", "Value.Input", When = "IsSuccess")]
                [CountFromResult("c.output", "Value.Output", When = "MaybeOk")]
                [CountFromResult("c.small", "Value.Small")]
                [CountFromResult("c.signed", "Value.Signed")]
                [CountFromResult("c.short", "Value.Short")]
                [CountFromResult("c.ushort", "Value.UShort")]
                [CountFromResult("c.uint", "Value.UInt")]
                [HistogramFromResult("h.big", "Value.Big")]
                [HistogramFromResult("h.ratio", "Value.Ratio")]
                [HistogramFromResult("h.score", "Value.Score")]
                [HistogramFromResult("h.cost", "Value.Cost")]
                [HistogramFromResult("h.maybeCost", "Value.MaybeCost")]
                [Count("c.calls", When = "IsSuccess")]
                [Histogram("h.ms", When = "IsSuccess")]
                Task<ClassResult> ClassAsync(CancellationToken ct);

                [CountFromResult("s.input", "Value.Input", When = "IsSuccess", Unit = "{token}", Description = "d")]
                [HistogramFromResult("s.cost", "Value.Cost", When = "IsSuccess")]
                [Count("s.calls", When = "IsSuccess")]
                [Histogram("s.ms", When = "IsSuccess")]
                ValueTask<StructResult> StructAsync(CancellationToken ct);

                [CountFromResult("n.value", "Value")]
                Task<int?> NullableAsync(CancellationToken ct);

                [HistogramFromResult("n.width", "Value.Width")]
                Task<Extent?> NullableStructAsync(CancellationToken ct);

                [CountFromResult("p.value", "")]
                Task<int> PlainAsync(CancellationToken ct);

                [HistogramFromResult("sync.score", "Score")]
                Usage Sync();
            }
        """;

    [Fact]
    public void ResultMetricsProbe_Compiles()
    {
        var errors = CompileWithGenerator(ResultMetricsProbeSource);

        Assert.True(
            errors.Length == 0,
            "Generated code did not compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    // A custom [AsyncMethodBuilder] task-like type, generic and not, with and without result
    // reads. The proxy has to be `async` over it and `await` the inner call, which only compiles
    // when the generator recognises the type as awaitable and unwraps T from GetResult.
    private const string TaskLikeProbeSource = """
            using ZeroAlloc.Telemetry;
            using System.Collections.Generic;
            using Pooled;

            public sealed class Batch
            {
                public bool IsComplete { get; set; }
                public int Size { get; set; }
            }

            [Instrument("MyApp.TaskLike")]
            public interface ITaskLikeProbe
            {
                [Trace("probe.pooled")]
                [Histogram("probe.pooled.ms")]
                PooledTask<List<int>> PlainAsync();

                [Trace("probe.pooledTagged")]
                [TraceTagFromResult("items.count", "Count")]
                [CountFromResult("probe.pooled.items", "Count")]
                PooledTask<List<int>> TaggedAsync();

                [Trace("probe.pooledGuarded")]
                [TraceTagFromResult("batch.size", "Size", When = "IsComplete")]
                [HistogramFromResult("probe.pooled.size", "Size", When = "IsComplete")]
                [Count("probe.pooled.complete", When = "IsComplete")]
                [Histogram("probe.pooled.guarded.ms", When = "IsComplete")]
                PooledTask<Batch?> GuardedAsync();

                [Trace("probe.pooledVoid")]
                [Count("probe.pooled.void")]
                [Histogram("probe.pooled.void.ms")]
                PooledTask RunAsync();

                PooledTask<string?> PassthroughAsync();
            }
        """ + TaskLikeTypes.Declarations;

    [Fact]
    public void TaskLikeProbe_Compiles()
    {
        var errors = CompileWithGenerator(TaskLikeProbeSource);

        Assert.True(
            errors.Length == 0,
            "Generated code did not compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    // dynamic results and arguments compiled on 1.6.4, where member paths were not resolved. A
    // path through dynamic cannot be checked, so every segment after it is emitted null-safe.
    private const string DynamicProbeSource = """
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            public sealed class Envelope { public dynamic Payload { get; set; } = null!; }

            [Instrument("MyApp.Dynamic")]
            public interface IDynamicProbe
            {
                [Trace("probe.dynamic")]
                [TraceTagFromResult("dyn.count", "Count")]
                [TraceTagFromResult("dyn.deep", "Inner.Items.Count")]
                [TraceTagFromResult("dyn.guarded", "Count", When = "IsOk")]
                [Count("dyn.calls", When = "IsOk")]
                Task<dynamic> GetAsync([TraceTag("arg.count", "Count")] dynamic arg);

                [Trace("probe.dynamicMember")]
                [TraceTagFromResult("env.count", "Payload.Count")]
                [Histogram("env.ms", When = "Payload.Ready")]
                Task<Envelope> EnvelopeAsync();
            }
        """;

    [Fact]
    public void DynamicProbe_Compiles()
    {
        var errors = CompileWithGenerator(DynamicProbeSource);

        Assert.True(
            errors.Length == 0,
            "Generated code did not compile:" + Environment.NewLine + string.Join(Environment.NewLine, errors));
    }

    private static string[] CompileWithGenerator(string source)
    {
        var (generatorDiagnostics, output) = GeneratorCompilation.Run(source);

        // Consumers build with TreatWarningsAsErrors, so a warning in the generated proxy or from
        // the generator is a broken build for them, not a cosmetic issue.
        var problems = new List<string>();
        foreach (var d in generatorDiagnostics)
        {
            if (d.Severity >= DiagnosticSeverity.Warning)
                problems.Add(d.ToString());
        }

        foreach (var d in output)
        {
            var inGenerated = d.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true;
            if (d.Severity == DiagnosticSeverity.Error
                || (inGenerated && d.Severity == DiagnosticSeverity.Warning))
            {
                problems.Add(d.ToString());
            }
        }

        return problems.ToArray();
    }
}
