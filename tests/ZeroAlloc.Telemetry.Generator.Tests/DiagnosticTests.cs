using System;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
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

    [Theory]
    [InlineData("[CountFromResult(\"x\", \"Length\")]", "CountFromResult")]
    [InlineData("[HistogramFromResult(\"x\", \"Length\")]", "HistogramFromResult")]
    public void ZTEL003_ResultMetricOnMethodWithoutInstrumentContainer_ProducesWarning(string attribute, string shortName)
    {
        var diagnostics = RunAndCollectDiagnostics($$"""
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            public class OrphanService
            {
                {{attribute}}
                public Task<string> GetAsync() => Task.FromResult("");
            }
            """);

        var d = Single(diagnostics, "ZTEL003");
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
        Assert.Equal("GetAsync", LocationText(d));
        Assert.Equal(
            $"[{shortName}] on 'OrphanService.GetAsync' is ignored — the containing type does not have [Instrument] applied, so no proxy is generated. Either apply [Instrument] to the enclosing interface or remove this attribute.",
            d.GetMessage(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// Tag attributes are ignored on a type without [Instrument] just as [Trace] is, so they are
    /// reported the same way rather than dropped without a word.
    /// </summary>
    [Theory]
    [InlineData("[TraceTagFromResult(\"t\", \"Length\")]", "TraceTagFromResult")]
    [InlineData("[TraceTagConstant(\"t\", \"web\")]", "TraceTagConstant")]
    public void ZTEL003_TagOnMethodWithoutInstrumentContainer_ProducesWarning(string attribute, string shortName)
    {
        var diagnostics = RunAndCollectDiagnostics($$"""
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            public class OrphanService
            {
                {{attribute}}
                public Task<string> GetAsync() => Task.FromResult("");
            }
            """);

        var d = Single(diagnostics, "ZTEL003");
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
        Assert.Equal("GetAsync", LocationText(d));
        Assert.Equal(
            $"[{shortName}] on 'OrphanService.GetAsync' is ignored — the containing type does not have [Instrument] applied, so no proxy is generated. Either apply [Instrument] to the enclosing interface or remove this attribute.",
            d.GetMessage(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("[TraceTagFromResult(\"t\", \"Length\")]")]
    [InlineData("[TraceTagConstant(\"t\", \"web\")]")]
    public void ZTEL003_TagInsideInstrumentedInterface_ProducesNoWarning(string attribute)
    {
        var diagnostics = RunAndCollectDiagnostics($$"""
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IProperService
            {
                [Trace("proper.get")]
                {{attribute}}
                Task<string> GetAsync();
            }
            """);

        Assert.Empty(diagnostics);
    }

    [Fact]
    public void ZTEL010_UnresolvedTraceTagPath_IsWarningAtTheArgument()
    {
        var source = ChunksSource("[TraceTag(\"batch.size\", \"Cuont\")]");
        var diagnostics = RunAndCollectDiagnostics(source);

        var d = Single(diagnostics, "ZTEL010");
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
        Assert.Equal("\"Cuont\"", LocationText(d));
        Assert.Equal(
            "'Cuont' in the path 'Cuont' is not a readable instance property or field of 'System.Collections.Generic.IReadOnlyList<string>', so [TraceTag] on 'chunks' records nothing. " + ParameterPathTail,
            d.GetMessage(CultureInfo.InvariantCulture));
        Assert.DoesNotContain(diagnostics, x => string.Equals(x.Id, "ZTEL007", StringComparison.Ordinal));
        AssertNoOutputErrors(source);
    }

    [Fact]
    public void ZTEL010_NamesTheSegmentAndTheTypeItWasLookedUpOn()
    {
        var source = ChunksSource("[TraceTag(\"batch.size\", member: \"Count.Digits\")]");
        var diagnostics = RunAndCollectDiagnostics(source);

        var d = Single(diagnostics, "ZTEL010");
        Assert.Equal("\"Count.Digits\"", LocationText(d));
        Assert.Equal(
            "'Digits' in the path 'Count.Digits' is not a readable instance property or field of 'int', so [TraceTag] on 'chunks' records nothing. " + ParameterPathTail,
            d.GetMessage(CultureInfo.InvariantCulture));
        AssertNoOutputErrors(source);
    }

    [Fact]
    public void ZTEL010_EmptySegment_IsNamedAsSuch()
    {
        var source = ChunksSource("[TraceTag(\"batch.size\", \"Count..Digits\")]");
        var diagnostics = RunAndCollectDiagnostics(source);

        var d = Single(diagnostics, "ZTEL010");
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
        Assert.Equal("\"Count..Digits\"", LocationText(d));
        Assert.Equal(
            "The path 'Count..Digits' has an empty segment, so [TraceTag] on 'chunks' records nothing. " + ParameterPathTail,
            d.GetMessage(CultureInfo.InvariantCulture));
        AssertNoOutputErrors(source);
    }

    [Theory]
    [InlineData("[TraceTag(\"batch.size\", \"Count\")]")]
    [InlineData("[TraceTag(\"batch\")]")]
    [InlineData("[TraceTag(\"batch\", \"\")]")]
    public void ZTEL010_ResolvedOrAbsentPath_ReportsNothing(string attribute)
    {
        var source = ChunksSource(attribute);

        Assert.Empty(RunAndCollectDiagnostics(source));
        AssertNoOutputErrors(source);
    }

    /// <summary>
    /// Without [Trace] no tag is emitted, so the path is never read and ZTEL004 already says the
    /// tag records nothing. A second warning about the path would only repeat it.
    /// </summary>
    [Fact]
    public void ZTEL010_WithoutTrace_ReportsOnlyZtel004()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            using System.Collections.Generic;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IChunks
            {
                Task StoreAsync([TraceTag("batch.size", "Cuont")] IReadOnlyList<string> chunks);
            }
            """;

        Assert.Equal(["ZTEL004"], RunAndCollectDiagnostics(source).Select(x => x.Id).ToArray());
        AssertNoOutputErrors(source);
    }

    /// <summary>
    /// ZTEL010 is a warning on code that built before it existed, so it has to be suppressible
    /// where it is reported, as any other warning is.
    /// </summary>
    [Fact]
    public void ZTEL010_IsSuppressedByPragma()
    {
        const string attribute = "[TraceTag(\"batch.size\", \"Cuont\")]";

        Assert.False(Single(EffectiveDiagnostics(ChunksSource(attribute)), "ZTEL010").IsSuppressed);

        var suppressed = ChunksSource(attribute)
            .Replace("[Instrument(", "#pragma warning disable ZTEL010\n[Instrument(", StringComparison.Ordinal);
        Assert.True(Single(EffectiveDiagnostics(suppressed), "ZTEL010").IsSuppressed);
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

        var d = Single(diagnostics, "ZTEL005");
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
        Assert.Equal("CreateAsync", LocationText(d));
        Assert.Equal(
            "[TraceTagFromResult] on 'IOrderService.CreateAsync' records nothing — the method returns void, Task or ValueTask, so there is no result to read. Remove it or return a value.",
            d.GetMessage(CultureInfo.InvariantCulture));

        // There is no result, so the path is not resolved against Task and no path error appears.
        Assert.DoesNotContain(diagnostics, x => string.Equals(x.Id, "ZTEL007", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("[TraceTagFromResult(\"t\", \"Totl\")]")]
    [InlineData("[TraceTagFromResult(\"t\", \"Total\", When = \"Totl\")]")]
    [InlineData("[CountFromResult(\"m\", \"Totl\")]")]
    [InlineData("[HistogramFromResult(\"m\", \"Totl\")]")]
    [InlineData("[CountFromResult(\"m\", \"Total\", When = \"Totl\")]")]
    [InlineData("[HistogramFromResult(\"m\", \"Total\", When = \"Totl\")]")]
    [InlineData("[Count(\"m\", When = \"Totl\")]")]
    [InlineData("[Histogram(\"m\", When = \"Totl\")]")]
    public void ZTEL007_UnresolvedSegment_IsReportedAtTheArgument(string attributes)
    {
        var source = TotalsSource(attributes);
        var diagnostics = RunAndCollectDiagnostics(source);

        var d = Single(diagnostics, "ZTEL007");
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal("\"Totl\"", LocationText(d));
        Assert.Equal(
            "'Totl' in the path 'Totl' is not a readable instance property or field of 'Totals'. " + PathNotFoundTail,
            d.GetMessage(CultureInfo.InvariantCulture));
        AssertNoOutputErrors(source);
    }

    [Fact]
    public void ZTEL007_NamesTheSegmentAndTheTypeItWasLookedUpOn()
    {
        var source = TotalsSource("[TraceTagFromResult(\"t\", \"Total.Digits\")]");
        var diagnostics = RunAndCollectDiagnostics(source);

        var d = Single(diagnostics, "ZTEL007");
        Assert.Equal("\"Total.Digits\"", LocationText(d));
        Assert.Equal(
            "'Digits' in the path 'Total.Digits' is not a readable instance property or field of 'int'. " + PathNotFoundTail,
            d.GetMessage(CultureInfo.InvariantCulture));
        AssertNoOutputErrors(source);
    }

    /// <summary>
    /// A static member, a set-only property and a member the proxy cannot see all exist, but none
    /// can be read from the result, so each is reported instead of emitted as code that fails.
    /// </summary>
    [Theory]
    [InlineData("Shared")]
    [InlineData("WriteOnly")]
    [InlineData("Hidden")]
    public void ZTEL007_UnreadableMember_IsReportedAsNotFound(string member)
    {
        var source = TotalsSource($"[TraceTagFromResult(\"t\", \"{member}\")]");
        var diagnostics = RunAndCollectDiagnostics(source);

        var d = Single(diagnostics, "ZTEL007");
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal($"\"{member}\"", LocationText(d));
        Assert.Equal(
            $"'{member}' in the path '{member}' is not a readable instance property or field of 'Totals'. " + PathNotFoundTail,
            d.GetMessage(CultureInfo.InvariantCulture));
        AssertNoOutputErrors(source);
    }

    [Fact]
    public void ZTEL007_EmptySegment_IsNamedAsSuch()
    {
        var source = TotalsSource("[TraceTagFromResult(\"t\", \"Total..Digits\")]");
        var diagnostics = RunAndCollectDiagnostics(source);

        var d = Single(diagnostics, "ZTEL007");
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal("\"Total..Digits\"", LocationText(d));
        Assert.Equal(
            "The path 'Total..Digits' has an empty segment. " + PathNotFoundTail,
            d.GetMessage(CultureInfo.InvariantCulture));
        AssertNoOutputErrors(source);
    }

    [Theory]
    [InlineData("TraceTagFromResult")]
    [InlineData("CountFromResult")]
    [InlineData("HistogramFromResult")]
    public void ZTEL007_AndZTEL008_AreBothReported_WhenPathAndGuardAreBothWrong(string attribute)
    {
        var source = TotalsSource($"[{attribute}(\"t\", \"Totl\", When = \"Count\")]");
        var diagnostics = RunAndCollectDiagnostics(source);

        Assert.Equal("\"Totl\"", LocationText(Single(diagnostics, "ZTEL007")));
        Assert.Equal("\"Count\"", LocationText(Single(diagnostics, "ZTEL008")));
        AssertNoOutputErrors(source);
    }

    /// <summary>
    /// Without [Trace] the writer emits no result tags, so a bad path or guard never reached the
    /// generated code and the method compiled. Only ZTEL004 applies there.
    /// </summary>
    [Fact]
    public void ZTEL004_BadPathAndGuardWithoutTrace_ReportOnlyZtel004()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            public sealed class Totals { public int Count { get; set; } }

            [Instrument("MyApp")]
            public interface ITotals
            {
                [Count("totals.count")]
                [TraceTagFromResult("t", "Totl", When = "Count")]
                Task<Totals> GetAsync();
            }
            """;

        var diagnostics = RunAndCollectDiagnostics(source);

        Assert.Equal(["ZTEL004"], diagnostics.Select(x => x.Id).ToArray());
        var d = diagnostics[0];
        Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
        Assert.Equal("GetAsync", LocationText(d));
        Assert.Equal(
            "[TraceTagFromResult] on 'ITotals.GetAsync' records nothing — the method has no [Trace], so no span is started to carry the tag. Add [Trace] to the method or remove the tag.",
            d.GetMessage(CultureInfo.InvariantCulture));
        AssertNoOutputErrors(source);
    }

    [Theory]
    [InlineData("[TraceTagFromResult(\"t\", \"Total\", When = \"Count\")]")]
    [InlineData("[CountFromResult(\"m\", \"Total\", When = \"Count\")]")]
    [InlineData("[HistogramFromResult(\"m\", \"Total\", When = \"Count\")]")]
    [InlineData("[Count(\"m\", When = \"Count\")]")]
    [InlineData("[Histogram(\"m\", When = \"Count\")]")]
    public void ZTEL008_NonBooleanWhen_ProducesError(string attributes)
    {
        var source = TotalsSource(attributes);
        var diagnostics = RunAndCollectDiagnostics(source);

        var d = Single(diagnostics, "ZTEL008");
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal("\"Count\"", LocationText(d));
        Assert.Equal(
            "When = 'Count' resolves to 'int'. A guard must name a member of type bool or bool? on the awaited return value, such as IsSuccess.",
            d.GetMessage(CultureInfo.InvariantCulture));
        AssertNoOutputErrors(source);
    }

    private const string CounterReason =
        "A counter adds long values, so the member must be sbyte, byte, short, ushort, int, uint or long, or a nullable form of one";

    private const string HistogramReason =
        "A histogram records double values, so the member must be sbyte, byte, short, ushort, int, uint, long, ulong, float, double or decimal, or a nullable form of one";

    [Theory]
    [InlineData("CountFromResult", "Text", "string", CounterReason)]
    [InlineData("CountFromResult", "Score", "double", CounterReason)]
    [InlineData("CountFromResult", "Big", "ulong", CounterReason)]
    [InlineData("HistogramFromResult", "Text", "string", HistogramReason)]
    [InlineData("HistogramFromResult", "Mode", "ReplyMode", HistogramReason)]
    public void ZTEL009_MemberThatDoesNotFit_ProducesError(string attribute, string member, string typeName, string reason)
    {
        var source = $$"""
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            public enum ReplyMode { Short, Long }

            public sealed class Reply
            {
                public string Text { get; set; } = "";
                public double Score { get; set; }
                public ulong Big { get; set; }
                public ReplyMode Mode { get; set; }
            }

            [Instrument("MyApp")]
            public interface IReplies
            {
                [{{attribute}}("reply.metric", "{{member}}")]
                Task<Reply> GetAsync();
            }
            """;
        var diagnostics = RunAndCollectDiagnostics(source);

        var d = Single(diagnostics, "ZTEL009");
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal($"\"{member}\"", LocationText(d));
        Assert.Equal(
            $"[{attribute}] cannot record '{member}' of type '{typeName}'. {reason}.",
            d.GetMessage(CultureInfo.InvariantCulture));
        AssertNoOutputErrors(source);
    }

    private const string DynamicReason =
        "The type of a dynamic member is only known at run time, so it cannot be checked against the instrument; expose a typed member instead";

    /// <summary>
    /// A path through <c>dynamic</c> resolves, since tags and guards compiled on 1.6.4, but a metric
    /// value there cannot be type-checked, so it is rejected rather than bound at run time.
    /// </summary>
    [Theory]
    [InlineData("CountFromResult", "Count", "Task<dynamic>")]
    [InlineData("HistogramFromResult", "Count", "Task<dynamic>")]
    [InlineData("CountFromResult", "Payload.Count", "Task<Envelope>")]
    [InlineData("HistogramFromResult", "Payload", "Task<Envelope>")]
    public void ZTEL009_MemberThroughDynamic_ProducesError(string attribute, string member, string returnType)
    {
        var source = $$"""
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            public sealed class Envelope { public dynamic Payload { get; set; } = null!; }

            [Instrument("MyApp")]
            public interface IDynamics
            {
                [{{attribute}}("dyn.metric", "{{member}}")]
                {{returnType}} GetAsync();
            }
            """;
        var diagnostics = RunAndCollectDiagnostics(source);

        var d = Single(diagnostics, "ZTEL009");
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal($"\"{member}\"", LocationText(d));
        Assert.Equal(
            $"[{attribute}] cannot record '{member}' of type 'dynamic'. {DynamicReason}.",
            d.GetMessage(CultureInfo.InvariantCulture));
        AssertNoOutputErrors(source);
    }

    /// <summary>A tag and a guard through <c>dynamic</c> resolve: no ZTEL007 and no ZTEL008.</summary>
    [Fact]
    public void DynamicResult_TagAndGuard_ReportNothing()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IDynamics
            {
                [Trace("dyn.get")]
                [TraceTagFromResult("dyn.count", "Count", When = "IsOk")]
                [Count("dyn.calls", When = "Status.IsOk")]
                Task<dynamic> GetAsync();
            }
            """;

        Assert.Empty(RunAndCollectDiagnostics(source));
        AssertNoOutputErrors(source);
    }

    [Fact]
    public void ZTEL009_EmptyMember_NamesTheReturnValue()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IReplies
            {
                [CountFromResult("reply.metric", "")]
                Task<string> GetAsync();
            }
            """;
        var diagnostics = RunAndCollectDiagnostics(source);

        var d = Single(diagnostics, "ZTEL009");
        Assert.Equal("\"\"", LocationText(d));
        Assert.Equal(
            $"[CountFromResult] cannot record the return value of type 'string'. {CounterReason}.",
            d.GetMessage(CultureInfo.InvariantCulture));
        AssertNoOutputErrors(source);
    }

    [Fact]
    public void ZTEL005_ResultReadsOnMethodWithoutResult_NameTheAttribute()
    {
        const string source = """
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IRunner
            {
                [CountFromResult("a", "Count")]
                [HistogramFromResult("b", "Count")]
                [Count("c", When = "IsSuccess")]
                [Histogram("d", When = "IsSuccess")]
                Task RunAsync();
            }
            """;
        var diagnostics = RunAndCollectDiagnostics(source);

        var ztel005 = diagnostics
            .Where(d => string.Equals(d.Id, "ZTEL005", StringComparison.Ordinal))
            .ToArray();

        Assert.All(ztel005, d =>
        {
            Assert.Equal(DiagnosticSeverity.Warning, d.Severity);
            Assert.Equal("RunAsync", LocationText(d));
        });

        const string tail = " on 'IRunner.RunAsync' records nothing — the method returns void, Task or ValueTask, so there is no result to read. Remove it or return a value.";
        // An array rather than a collection expression: BeEquivalentTo has both params and
        // IEnumerable overloads, and a collection expression would be ambiguous between them.
        ztel005.Select(d => d.GetMessage(CultureInfo.InvariantCulture)).Should().BeEquivalentTo(new[]
        {
            "[Count(When = \"IsSuccess\")]" + tail,
            "[Histogram(When = \"IsSuccess\")]" + tail,
            "[CountFromResult]" + tail,
            "[HistogramFromResult]" + tail,
        });

        // Nothing is resolved against Task, so no path or type errors follow.
        Assert.DoesNotContain(diagnostics, d => d.Id is "ZTEL007" or "ZTEL008" or "ZTEL009");
        AssertNoOutputErrors(source);
    }

    [Fact]
    public void ZTEL008_NullableBoolWhen_ProducesNoError()
    {
        var diagnostics = RunAndCollectDiagnostics(TotalsSource("[TraceTagFromResult(\"t\", \"Total\", When = \"MaybeOk\")]"));

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZTEL007", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZTEL008", StringComparison.Ordinal));
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

    /// <summary>The one diagnostic with <paramref name="id"/>; fails listing every diagnostic otherwise.</summary>
    private static Diagnostic Single(Diagnostic[] diagnostics, string id)
    {
        var matches = Array.FindAll(diagnostics, d => string.Equals(d.Id, id, StringComparison.Ordinal));
        Assert.True(
            matches.Length == 1,
            $"Expected exactly one {id}, found {matches.Length.ToString(CultureInfo.InvariantCulture)}:"
                + Environment.NewLine + string.Join(Environment.NewLine, diagnostics.Select(x => x.ToString())));
        return matches[0];
    }

    /// <summary>
    /// The generator's diagnostic must be the only error: the invalid tag is not emitted, so the
    /// generated proxy compiles.
    /// </summary>
    private static void AssertNoOutputErrors(string source) =>
        Assert.Empty(GeneratorCompilation.OutputErrors(source));

    /// <summary>The source text a diagnostic points at, so tests assert the exact location.</summary>
    private static string LocationText(Diagnostic d) =>
        d.Location.SourceTree!.GetText().ToString(d.Location.SourceSpan);

    private const string PathNotFoundTail =
        "Each segment of a member path names a property or field of the type reached so far, starting from the awaited return value.";

    private const string ParameterPathTail =
        "Each segment of a member path names a property or field of the type reached so far, starting from the parameter.";

    private static string ChunksSource(string attribute) => $$"""
        using ZeroAlloc.Telemetry;
        using System.Collections.Generic;
        using System.Threading.Tasks;

        [Instrument("MyApp")]
        public interface IChunks
        {
            [Trace("chunks.store")]
            Task StoreAsync({{attribute}} IReadOnlyList<string> chunks);
        }
        """;

    /// <summary>
    /// The generator's diagnostics after the compiler applies <c>#pragma warning</c>. One the
    /// pragma disables comes back with <see cref="Diagnostic.IsSuppressed"/> set, and a build
    /// does not report it.
    /// </summary>
    private static Diagnostic[] EffectiveDiagnostics(string source)
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var diagnostics = CSharpGeneratorDriver.Create(new InstrumentGenerator())
            .RunGenerators(compilation).GetRunResult().Diagnostics;

        return CompilationWithAnalyzers.GetEffectiveDiagnostics(diagnostics, compilation).ToArray();
    }

    private static string TotalsSource(string attributes) => $$"""
        using ZeroAlloc.Telemetry;
        using System.Threading.Tasks;

        public sealed class Totals
        {
            public int Total { get; set; }
            public int Count { get; set; }
            public bool? MaybeOk { get; set; }
            public static int Shared { get; set; }
            public int WriteOnly { set { } }
            private int Hidden { get; set; }
        }

        [Instrument("MyApp")]
        public interface ITotals
        {
            [Trace("totals.get")]
            {{attributes}}
            Task<Totals> GetAsync();
        }
        """;

    private static MetadataReference[] References()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        return trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location))
            .ToArray();
    }

    private static Diagnostic[] RunAndCollectDiagnostics(string source)
    {
        var compilation = CSharpCompilation.Create("TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            References(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var driver = CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(compilation);
        return driver.GetRunResult().Diagnostics.ToArray();
    }

    [Fact]
    public void ZTEL006_UnknownTokenInSpanName_ProducesWarning()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IStore
            {
                [Trace("store.save.{Type}")]
                Task SaveAsync();
            }
            """);

        Assert.Contains(diagnostics, d => string.Equals(d.Id, "ZTEL006", StringComparison.Ordinal) && d.Severity == DiagnosticSeverity.Warning);
    }

    [Fact]
    public void ZTEL006_RecognisedTypeToken_ProducesNoWarning()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IStore
            {
                [Trace("store.save.{type}")]
                Task SaveAsync();
            }
            """);

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZTEL006", StringComparison.Ordinal));
    }

    [Fact]
    public void ZTEL006_ConstantSpanName_ProducesNoWarning()
    {
        var diagnostics = RunAndCollectDiagnostics("""
            using ZeroAlloc.Telemetry;
            using System.Threading.Tasks;

            [Instrument("MyApp")]
            public interface IStore
            {
                [Trace("store.save")]
                Task SaveAsync();
            }
            """);

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZTEL006", StringComparison.Ordinal));
    }
}
