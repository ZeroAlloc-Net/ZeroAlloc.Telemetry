# Metrics from the Result Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Ship ZeroAlloc.Telemetry 1.7.0 with `[CountFromResult]`, `[HistogramFromResult]`, and `When`/`Unit`/`Description` on `[Count]` and `[Histogram]`. Member paths and guards are validated in the generator. The existing bugs this code touches are fixed along the way.

**Architecture:** The generator already resolves dotted paths and `When` guards for `[TraceTagFromResult]`. That resolver moves into its own `PathResolver` and learns to say where a path stops resolving, which gives ZTEL007 and ZTEL008. The new instruments reuse it. The writer's metric fields move to a `MetricFieldTable`, keyed by instrument kind plus metric name. Result-driven instruments are emitted next to the result tags, inside the same `try`, and read through the same `_tagged` copy.

**Tech Stack:** C# / Roslyn incremental source generator (netstandard2.0, Microsoft.CodeAnalysis.CSharp 5.9.0), `System.Diagnostics.Metrics`, xUnit 2.9.3, AwesomeAssertions 9.6.0, ZeroAlloc.TestHelpers `GeneratorSnapshot`, .NET SDK 10.0.401.

**Spec:** `docs/plans/2026-09-26-metrics-from-result-design.md`

## Global Constraints

Copied verbatim from the spec:

- **Release:** ZeroAlloc.Telemetry 1.7.0, a minor.
- `CountAttribute` and `HistogramAttribute` gain `When`, `Unit` and `Description`, all `{ get; set; }` and defaulting to null. Every new member goes into `PublicAPI.Unshipped.txt`.
- **Reuse `[TraceTagFromResult]`'s path and guard machinery** for the new attributes. That gives one resolver, one set of nullable rules and one meaning of `When`.
- **Validate paths and guards in the generator.**
- **Defer `[MetricTagFromResult]`,** which adds a dimension, to a follow-up issue.
- The new IDs continue from ZTEL006. All are reported at the attribute's argument location.
- A path error used to be a compile error in generated code, so ZTEL007 and ZTEL008 turn an existing hard error into a clearer one. No existing code that compiled stops compiling.
- **No `[Trace]` needed.** Unlike result tags, the new attributes don't require `[Trace]`: they are metrics, not span data.
- **Rules:** `AnalyzerReleases.Unshipped.md` gets ZTEL007 to ZTEL009, plus the ZTEL005 message change if the release file tracks it.
- One PR closes #142, released as 1.7.0.
- The PR body carries a `BEGIN_COMMIT_OVERRIDE` block.
- Commit bodies have lines of at most 100 characters and no nested parentheses.

Repo rules, which apply to every task:

- `TreatWarningsAsErrors` is on for every project, and RS0016/RS0017 are errors (`Directory.Build.props`). A new public member without its `PublicAPI.Unshipped.txt` line fails the build.
- **No analyzer suppressions.** No `#pragma warning disable`, `[SuppressMessage]` or `<NoWarn>` added anywhere, and that includes tests. If an analyzer fires on emitted code, change the code the generator emits. If it fires on hand-written code, fix that code.
- The analyzers in `Directory.Build.props` apply to the generator and the tests too. That covers Meziantou, including MA0048 (one type per file, named after the file), Roslynator, ErrorProne.NET, NetFabric.Hyperlinq and ZeroAlloc.Analyzers. Use `string.Equals(a, b, StringComparison.Ordinal)`, `StringComparer.Ordinal`, and `ToString(CultureInfo.InvariantCulture)` for numbers.
- Commits are conventional. Valid scopes are `core`, `generator`, `docs`, `ci` and `deps`, and the header is at most 100 characters. Body lines are at most 100 characters and contain no nested parentheses: never write `typeof(X)` or `nameof(X)` inside a parenthesised phrase. No `claude.ai/code/session_` URL anywhere. Every commit ends with `Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>`.
- Never add an optional parameter to a shipped signature, because api-compat guards every package. This plan only adds properties and types.
- Snapshots are accepted by re-running with `ZA_SNAPSHOT_UPDATE=1`. In PowerShell that is `$env:ZA_SNAPSHOT_UPDATE='1'; dotnet test ...; Remove-Item Env:ZA_SNAPSHOT_UPDATE`. A mismatch writes a `.received.cs` next to the snapshot. Review every accepted snapshot against the expected text in this plan before committing, and never commit `.received.*` files.
- All commands run from the repo root, `C:\wt\telemetry-metrics`, in Git Bash.

---

## File Structure

| File | Status | Responsibility |
|---|---|---|
| `src/ZeroAlloc.Telemetry/CountAttribute.cs` | modify | `When`, `Unit`, `Description` |
| `src/ZeroAlloc.Telemetry/HistogramAttribute.cs` | modify | `When`, `Unit`, `Description` |
| `src/ZeroAlloc.Telemetry/CountFromResultAttribute.cs` | create | new attribute |
| `src/ZeroAlloc.Telemetry/HistogramFromResultAttribute.cs` | create | new attribute |
| `src/ZeroAlloc.Telemetry/InstrumentAttribute.cs` | modify | doc example that compiles |
| `src/ZeroAlloc.Telemetry/PublicAPI.Unshipped.txt` | modify | new API lines |
| `src/ZeroAlloc.Telemetry.Generator/Models/MetricKind.cs` | create | `Counter` / `Histogram` |
| `src/ZeroAlloc.Telemetry.Generator/Models/MetricModel.cs` | create | one instrument use on one method |
| `src/ZeroAlloc.Telemetry.Generator/Models/MethodModel.cs` | modify | `Count`, `Histogram`, `ResultMetrics` |
| `src/ZeroAlloc.Telemetry.Generator/MetricFieldTable.cs` | create | field per (kind, metric), with valid and distinct identifiers |
| `src/ZeroAlloc.Telemetry.Generator/TaskShapes.cs` | create | Task/ValueTask detection by symbol |
| `src/ZeroAlloc.Telemetry.Generator/PathResolver.cs` | create | shared path resolver that reports where a path stops |
| `src/ZeroAlloc.Telemetry.Generator/AttributeLocations.cs` | create | location of an attribute argument |
| `src/ZeroAlloc.Telemetry.Generator/InstrumentGenerator.cs` | modify | model building, validation, ZTEL003 registrations |
| `src/ZeroAlloc.Telemetry.Generator/ProxyWriter.cs` | modify | emission |
| `src/ZeroAlloc.Telemetry.Generator/InstrumentDiagnostics.cs` | modify | ZTEL003 title, ZTEL005 reword, ZTEL007–009 |
| `src/ZeroAlloc.Telemetry.Generator/AnalyzerReleases.Unshipped.md` | modify | ZTEL007–009 |
| `tests/ZeroAlloc.Telemetry.Generator.Tests/MetricFieldTests.cs` | create | field-name and unit/description snapshots |
| `tests/ZeroAlloc.Telemetry.Generator.Tests/ResultMetricTests.cs` | create | result-driven instrument snapshots |
| `tests/ZeroAlloc.Telemetry.Generator.Tests/{DiagnosticTests,GeneratedCodeCompilesTests,TraceTests,TraceTagTests}.cs` | modify | new cases |
| `tests/ZeroAlloc.Telemetry.Generated.Tests/*` | create | generator-backed runtime tests using `MeterListener` |
| `ZeroAlloc.Telemetry.slnx` | modify | add the runtime test project |
| `samples/ZeroAlloc.Telemetry.AotSmoke/*` | modify / create | one result-driven counter |
| `docs/attributes.md`, `docs/source-generator.md`, `docs/index.md`, `README.md` | modify | docs |

---

### Task 1: Metric fields keyed by kind, with valid, distinct identifiers and escaped literals

**Files:**
- Create: `src/ZeroAlloc.Telemetry.Generator/Models/MetricKind.cs`
- Create: `src/ZeroAlloc.Telemetry.Generator/Models/MetricModel.cs`
- Create: `src/ZeroAlloc.Telemetry.Generator/MetricFieldTable.cs`
- Modify: `src/ZeroAlloc.Telemetry.Generator/Models/MethodModel.cs:15-28`
- Modify: `src/ZeroAlloc.Telemetry.Generator/InstrumentGenerator.cs:187-217`, which is `BuildMethods`
- Modify: `src/ZeroAlloc.Telemetry.Generator/ProxyWriter.cs`: `Write` (8-48), `WriteMetricFields` (88-105), `WriteMethod` (114-141), `WriteSpanStartAndTags` (146-184), `WriteInstrumentedBody` (186-221), `WriteResultTags` (233-282), `WriteCatchBlock` (284-300) and `ToFieldName` (310-311)
- Create: `tests/ZeroAlloc.Telemetry.Generator.Tests/MetricFieldTests.cs`
- Modify: `tests/ZeroAlloc.Telemetry.Generator.Tests/GeneratedCodeCompilesTests.cs`

**Interfaces:**
- Consumes: nothing new.
- Produces:
  - `internal enum MetricKind { Counter, Histogram }`
  - `internal sealed record MetricModel(MetricKind Kind, string Metric)`. Task 4 and Task 5 add optional parameters.
  - `MethodModel` now has `MetricModel? Count` and `MetricModel? Histogram` in place of `string? CountMetric` and `string? HistogramMetric`, at the same positions.
  - `internal sealed class MetricFieldTable` with `static MetricFieldTable Build(InstrumentModel model)`, `IReadOnlyList<MetricFieldTable.Field> Fields` and `string FieldFor(MetricModel metric)`.
  - `internal sealed record MetricFieldTable.Field(MetricKind Kind, string Metric, string FieldName)`.
  - `ProxyWriter.Literal(string value) -> string`, which is private.

- [ ] **Step 1: Write the failing snapshot and text tests**

Create `tests/ZeroAlloc.Telemetry.Generator.Tests/MetricFieldTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Metric fields: one static field per instrument kind and metric name, each with a valid and
/// distinct identifier, and each name emitted as an escaped literal.
/// </summary>
public class MetricFieldTests
{
    private const string CollidingSource = """
        using ZeroAlloc.Telemetry;
        using System.Threading;
        using System.Threading.Tasks;

        [Instrument("MyApp.Fields")]
        public interface IFields
        {
            // Same name, different kinds: 1.6.4 emitted only the counter, so the histogram
            // field was missing and the proxy did not compile.
            [Count("x")]
            Task<int> CountedAsync(CancellationToken ct);

            [Histogram("x")]
            Task<int> TimedAsync(CancellationToken ct);

            // Distinct names that sanitize to the same identifier.
            [Count("a.b")]
            Task<int> DottedAsync(CancellationToken ct);

            [Count("a_b")]
            Task<int> UnderscoredAsync(CancellationToken ct);

            // Characters that are not valid in an identifier, and a quote that must be escaped.
            [Count("http/requests total")]
            Task<int> SlashAsync(CancellationToken ct);

            [Count("say \"hi\"")]
            Task<int> QuotedAsync(CancellationToken ct);

            // Names that would collide with the proxy's own members and locals.
            [Count("meter")]
            Task<int> MeterAsync(CancellationToken ct);

            [Histogram("result")]
            Task<int> ResultAsync(CancellationToken ct);

            // Same kind and name on another method: still one field.
            [Count("x")]
            Task<int> CountedAgainAsync(CancellationToken ct);
        }
        """;

    [Fact]
    public void GeneratesDistinctFields_ForCollidingMetricNames()
        => GeneratorSnapshot.Verify(RunGenerator(CollidingSource));

    /// <summary>
    /// Asserts the field block directly, so a snapshot approved while wrong cannot lock the
    /// collision back in.
    /// </summary>
    [Fact]
    public void EmitsOneFieldPerKindAndName()
    {
        var generated = RunGeneratorSource(CollidingSource);

        Assert.Contains("Counter<long> _x = _meter.CreateCounter<long>(\"x\");", generated, StringComparison.Ordinal);
        Assert.Contains("Histogram<double> _x_2 = _meter.CreateHistogram<double>(\"x\");", generated, StringComparison.Ordinal);
        Assert.Contains("Counter<long> _a_b = _meter.CreateCounter<long>(\"a.b\");", generated, StringComparison.Ordinal);
        Assert.Contains("Counter<long> _a_b_2 = _meter.CreateCounter<long>(\"a_b\");", generated, StringComparison.Ordinal);
        Assert.Contains("Counter<long> _http_requests_total = _meter.CreateCounter<long>(\"http/requests total\");", generated, StringComparison.Ordinal);
        Assert.Contains("Counter<long> _say__hi_ = _meter.CreateCounter<long>(\"say \\\"hi\\\"\");", generated, StringComparison.Ordinal);
        Assert.Contains("Counter<long> _metric_meter = _meter.CreateCounter<long>(\"meter\");", generated, StringComparison.Ordinal);
        Assert.Contains("Histogram<double> _metric_result = _meter.CreateHistogram<double>(\"result\");", generated, StringComparison.Ordinal);
        Assert.Contains("_metric_result.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);", generated, StringComparison.Ordinal);

        // Two [Count("x")] methods, one field.
        Assert.Equal(1, CountOccurrences(generated, "Counter<long> _x ="));
    }

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static string RunGeneratorSource(string source)
    {
        // Plain loop rather than LINQ: EPS06 flags Select over ImmutableArray as a hidden copy.
        var sb = new System.Text.StringBuilder();
        foreach (var tree in RunGenerator(source).GetRunResult().GeneratedTrees)
            sb.AppendLine(tree.ToString());

        return sb.ToString();
    }

    private static GeneratorDriver RunGenerator(string source)
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        var runtimeRefs = trustedPlatformAssemblies
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => MetadataReference.CreateFromFile(p))
            .ToArray();

        var compilation = CSharpCompilation.Create("TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            runtimeRefs.Concat<MetadataReference>(
            [
                MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location),
            ]),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(compilation);
    }
}
```

In `tests/ZeroAlloc.Telemetry.Generator.Tests/GeneratedCodeCompilesTests.cs`, add a second probe and a fact after `GeneratedOutput_Compiles`. The quoted names cover every string the writer emits as a literal: the activity source, the span name, a constant tag, a result tag and a parameter tag.

```csharp
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
```

- [ ] **Step 2: Run the tests to verify they fail**

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~MetricFieldTests|FullyQualifiedName~FieldNameProbe_Compiles"`

Expected: FAIL.
- `EmitsOneFieldPerKindAndName` fails on the `_x_2` assertion.
- `GeneratesDistinctFields_ForCollidingMetricNames` fails with `missing snapshot 'MetricFieldTests.GeneratesDistinctFields_ForCollidingMetricNames#FieldsInstrumented.g.verified.cs'`.
- `FieldNameProbe_Compiles` fails with CS0103 for the missing histogram field, plus CS1003/CS1026 syntax errors from the unescaped quotes.

- [ ] **Step 3: Add the metric model**

Create `src/ZeroAlloc.Telemetry.Generator/Models/MetricKind.cs`:

```csharp
namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>The instrument a metric is recorded on, which is also the type of its field.</summary>
internal enum MetricKind
{
    /// <summary><c>Counter&lt;long&gt;</c>.</summary>
    Counter,

    /// <summary><c>Histogram&lt;double&gt;</c>.</summary>
    Histogram,
}
```

Create `src/ZeroAlloc.Telemetry.Generator/Models/MetricModel.cs`:

```csharp
namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>One use of an instrument by one method.</summary>
/// <param name="Kind">Which instrument, and so which field type.</param>
/// <param name="Metric">The metric name passed to the <c>Meter</c> factory.</param>
internal sealed record MetricModel(MetricKind Kind, string Metric);
```

Replace the record in `src/ZeroAlloc.Telemetry.Generator/Models/MethodModel.cs`. Keep the existing `<param>` docs and add two:

```csharp
/// <param name="Count"><c>[Count]</c> on the method, or null.</param>
/// <param name="Histogram"><c>[Histogram]</c> on the method, or null.</param>
internal sealed record MethodModel(
    string Name,
    string ReturnType,
    bool IsAsync,
    bool ReturnsVoid,
    IReadOnlyList<ParameterModel> Parameters,
    string? TraceName,
    MetricModel? Count,
    MetricModel? Histogram,
    IReadOnlyList<ResultTagModel> ResultTags,
    bool ResultCanBeNull,
    IReadOnlyList<ConstantTagModel> ConstantTags,
    string? TraceNameExpression = null
);
```

- [ ] **Step 4: Add `MetricFieldTable`**

Create `src/ZeroAlloc.Telemetry.Generator/MetricFieldTable.cs`:

```csharp
using System.Globalization;
using System.Text;
using ZeroAlloc.Telemetry.Generator.Models;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// Assigns one static field per distinct instrument kind and metric name on a proxy. Each
/// identifier is valid C#, and distinct from every other field, member and local the proxy emits.
/// </summary>
/// <remarks>
/// <para>
/// Keying by name alone, as 1.6.4 did, dropped the histogram field whenever a counter shared its
/// name, and the proxy then referenced a field that did not exist. Deriving the identifier by
/// replacing only <c>.</c> and <c>-</c> collapsed <c>a.b</c> and <c>a_b</c> onto one field, and
/// left every other punctuation character in place, which is not a valid identifier.
/// </para>
/// <para>
/// A metric name can also collide with the proxy's own names. <c>meter</c> would redeclare
/// <c>_meter</c>, and <c>result</c> would be shadowed inside every method by the <c>_result</c>
/// local. Those are moved to a <c>_metric_</c> prefix before deduplication.
/// </para>
/// </remarks>
internal sealed class MetricFieldTable
{
    /// <summary>A field the proxy declares.</summary>
    /// <param name="Kind">Field type.</param>
    /// <param name="Metric">Metric name passed to the <c>Meter</c> factory.</param>
    /// <param name="FieldName">The full identifier, including its leading underscore.</param>
    internal sealed record Field(MetricKind Kind, string Metric, string FieldName);

    private readonly record struct MetricKey(MetricKind Kind, string Metric);

    // Members and locals the writer emits. A field with one of these names either fails to
    // compile or is shadowed by the local inside every method body.
    private static readonly HashSet<string> ReservedNames = new(StringComparer.Ordinal)
    {
        "_activitySource", "_meter", "_inner", "_activity", "_sw", "_result", "_tagged", "_ex", "_implName",
    };

    private readonly Dictionary<MetricKey, int> _indexByKey;
    private readonly List<Field> _fields;

    private MetricFieldTable(List<Field> fields, Dictionary<MetricKey, int> indexByKey)
    {
        _fields = fields;
        _indexByKey = indexByKey;
    }

    /// <summary>Fields in first-use order: interface member order, then attribute order.</summary>
    public IReadOnlyList<Field> Fields => _fields;

    /// <summary>The identifier of the field that records <paramref name="metric"/>.</summary>
    public string FieldFor(MetricModel metric) =>
        _fields[_indexByKey[new MetricKey(metric.Kind, metric.Metric)]].FieldName;

    public static MetricFieldTable Build(InstrumentModel model)
    {
        var fields = new List<Field>();
        var indexByKey = new Dictionary<MetricKey, int>();
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var method in model.Methods)
        {
            foreach (var metric in MetricsOf(method))
            {
                var key = new MetricKey(metric.Kind, metric.Metric);
                if (indexByKey.ContainsKey(key))
                    continue;

                indexByKey.Add(key, fields.Count);
                fields.Add(new Field(metric.Kind, metric.Metric, AssignName(metric.Metric, used)));
            }
        }

        return new MetricFieldTable(fields, indexByKey);
    }

    private static IEnumerable<MetricModel> MetricsOf(MethodModel method)
    {
        if (method.Count is { } count)
            yield return count;

        if (method.Histogram is { } histogram)
            yield return histogram;
    }

    private static string AssignName(string metric, HashSet<string> used)
    {
        var candidate = "_" + Sanitize(metric);
        if (IsReserved(candidate))
            candidate = "_metric" + candidate;

        var name = candidate;
        for (var n = 2; !used.Add(name); n++)
            name = candidate + "_" + n.ToString(CultureInfo.InvariantCulture);

        return name;
    }

    /// <summary>Keeps letters, digits and underscores; every other character becomes an underscore.</summary>
    private static string Sanitize(string metric)
    {
        var sb = new StringBuilder(metric.Length);
        foreach (var c in metric)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');

        return sb.ToString();
    }

    private static bool IsReserved(string name) =>
        ReservedNames.Contains(name)
        || name.StartsWith("_spanName_", StringComparison.Ordinal)
        || name.StartsWith("_tag_", StringComparison.Ordinal);
}
```

- [ ] **Step 5: Build the model in the generator**

In `src/ZeroAlloc.Telemetry.Generator/InstrumentGenerator.cs`, `BuildMethods`, replace the `methods.Add(new MethodModel(...))` call:

```csharp
            methods.Add(new MethodModel(
                member.Name,
                returnType,
                isAsync,
                returnsVoid,
                parameters,
                traceName,
                countMetric is null ? null : new MetricModel(MetricKind.Counter, countMetric),
                histMetric is null ? null : new MetricModel(MetricKind.Histogram, histMetric),
                resultTags,
                ResultCanBeNull(member),
                constantTags,
                BuildTraceNameExpression(traceName)));
```

- [ ] **Step 6: Emit through the table and escape every literal**

In `src/ZeroAlloc.Telemetry.Generator/ProxyWriter.cs`, make the following changes.

In `Write`, replace lines 35-44:

```csharp
        sb.AppendLine($"    private static readonly ActivitySource _activitySource = new({Literal(model.ActivitySourceName)});");
        sb.AppendLine($"    private static readonly Meter _meter = new({Literal(model.ActivitySourceName)});");

        var fields = MetricFieldTable.Build(model);
        WriteMetricFields(sb, fields);

        sb.AppendLine();
        WriteFieldsAndConstructor(sb, model);

        for (var i = 0; i < model.Methods.Count; i++)
            WriteMethod(sb, model.Methods[i], i, fields);
```

Replace `WriteMetricFields`:

```csharp
    private static void WriteMetricFields(StringBuilder sb, MetricFieldTable fields)
    {
        foreach (var field in fields.Fields)
        {
            var (type, factory) = field.Kind == MetricKind.Counter
                ? ("Counter<long>", "CreateCounter<long>")
                : ("Histogram<double>", "CreateHistogram<double>");

            sb.AppendLine($"    private static readonly {type} {field.FieldName} = _meter.{factory}({Literal(field.Metric)});");
        }
    }
```

In `WriteMethod`, change the signature to `WriteMethod(StringBuilder sb, MethodModel method, int index, MetricFieldTable fields)`. Replace `method.HistogramMetric is not null` with `method.Histogram is not null`. Replace the `needsTry` expression:

```csharp
        var needsTry = method.TraceName is not null
                    || method.Histogram is not null
                    || method.Count is not null;
```

Pass the table on with `WriteInstrumentedBody(sb, method, argList, fields);`.

In `WriteSpanStartAndTags`, route the span name and both tag names through `Literal`:

```csharp
        var spanName = method.TraceNameExpression is not null
            ? SpanNameField(method, index)
            : Literal(method.TraceName!);
```
```csharp
            sb.AppendLine($"        _activity?.SetTag({Literal(constant.TagName)}, {constant.Literal});");
```
```csharp
            sb.AppendLine($"        _activity?.SetTag({Literal(p.TagName)}, {access});");
```

In `WriteInstrumentedBody`, change the signature to `(StringBuilder sb, MethodModel method, string argList, MetricFieldTable fields)` and replace lines 204-214:

```csharp
        if (method.Count is { } count)
            sb.AppendLine($"            {fields.FieldFor(count)}.Add(1);");

        if (method.Histogram is { } histogram)
            sb.AppendLine($"            {fields.FieldFor(histogram)}.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);");
```

Also replace its `WriteCatchBlock(sb, method);` with `WriteCatchBlock(sb, method, fields);`.

In `WriteResultTags`, the two `SetTag` lines become:

```csharp
                sb.AppendLine($"                _activity?.SetTag({Literal(tag.TagName)}, {access});");
```
```csharp
                sb.AppendLine($"            _activity?.SetTag({Literal(tag.TagName)}, {access});");
```

Replace `WriteCatchBlock`:

```csharp
    private static void WriteCatchBlock(StringBuilder sb, MethodModel method, MetricFieldTable fields)
    {
        sb.AppendLine("        catch (Exception _ex)");
        sb.AppendLine("        {");

        if (method.TraceName is not null)
            sb.AppendLine("            _activity?.SetStatus(ActivityStatusCode.Error, _ex.Message);");

        if (method.Histogram is { } histogram)
            sb.AppendLine($"            {fields.FieldFor(histogram)}.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);");

        sb.AppendLine("            throw;");
        sb.AppendLine("        }");
    }
```

Delete `ToFieldName` and add:

```csharp
    /// <summary>
    /// A name as a C# string literal, escaped the way the language writes it. A quote or a
    /// backslash in a metric, span or tag name would otherwise emit code that does not compile.
    /// </summary>
    private static string Literal(string value) =>
        Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(value, quote: true);
```

- [ ] **Step 7: Run the tests and accept the new snapshot**

Run: `ZA_SNAPSHOT_UPDATE=1 dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~MetricFieldTests"`

Open `tests/ZeroAlloc.Telemetry.Generator.Tests/Snapshots/MetricFieldTests.GeneratesDistinctFields_ForCollidingMetricNames#FieldsInstrumented.g.verified.cs`. Its field block must read exactly:

```csharp
internal sealed class FieldsInstrumented : IFields
{
    private static readonly ActivitySource _activitySource = new("MyApp.Fields");
    private static readonly Meter _meter = new("MyApp.Fields");
    private static readonly Counter<long> _x = _meter.CreateCounter<long>("x");
    private static readonly Histogram<double> _x_2 = _meter.CreateHistogram<double>("x");
    private static readonly Counter<long> _a_b = _meter.CreateCounter<long>("a.b");
    private static readonly Counter<long> _a_b_2 = _meter.CreateCounter<long>("a_b");
    private static readonly Counter<long> _http_requests_total = _meter.CreateCounter<long>("http/requests total");
    private static readonly Counter<long> _say__hi_ = _meter.CreateCounter<long>("say \"hi\"");
    private static readonly Counter<long> _metric_meter = _meter.CreateCounter<long>("meter");
    private static readonly Histogram<double> _metric_result = _meter.CreateHistogram<double>("result");
```

`CountedAgainAsync` must call `_x.Add(1);`, and `ResultAsync` must call `_metric_result.Record(...)` in both the `try` and the `catch`.

- [ ] **Step 8: Run the whole generator suite without update mode**

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj`

Expected: `Passed!  - Failed:     0`. Every existing snapshot is unchanged, because `orders.created` still becomes `_orders_created` and a plain name's literal is the same text as before. Confirm with `git status --short tests/`: the only snapshot change is the new `MetricFieldTests...verified.cs`.

- [ ] **Step 9: Commit**

```bash
git add src/ZeroAlloc.Telemetry.Generator tests/ZeroAlloc.Telemetry.Generator.Tests
git commit -F - <<'EOF'
fix(generator): key metric fields by kind and emit valid, distinct identifiers

[Count] and [Histogram] with the same metric name shared one set of emitted names, so the
histogram field was never declared and the proxy failed to compile. Names that sanitize alike,
such as a.b and a_b, collapsed onto one field, and any character other than . and - produced
an invalid identifier.

Fields are now keyed by instrument kind plus metric name. Identifiers are sanitized, kept clear
of the proxy's own members and locals, and made distinct with a numeric suffix. Every name the
proxy emits as a string literal is now escaped, so a quote in a metric, span or tag name no
longer breaks the build.

Refs #142

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 2: Catch variable only when read, and async detection by symbol

Two `fix:` commits. The catch fix goes first, so the async snapshot is accepted once, in its final form.

**Files:**
- Create: `src/ZeroAlloc.Telemetry.Generator/TaskShapes.cs`
- Modify: `src/ZeroAlloc.Telemetry.Generator/ProxyWriter.cs`, `WriteCatchBlock`
- Modify: `src/ZeroAlloc.Telemetry.Generator/InstrumentGenerator.cs`: `BuildMethods` (191-196), `ResultCanBeNull` (312-328), `UnwrapAwaited` (331-344) and `BuildResultTags` (582)
- Modify: `tests/ZeroAlloc.Telemetry.Generator.Tests/GeneratedCodeCompilesTests.cs`
- Modify: `tests/ZeroAlloc.Telemetry.Generator.Tests/TraceTests.cs`
- Snapshots that change, catch line only:
  - `TraceTests.GeneratesCounterIncrement_ForCountMethod#…`
  - `TraceTests.GeneratesHistogramRecord_ForHistogramMethod#…`
  - `TraceTagTests.GeneratesNoTags_WhenMethodHasNoTrace#…`
  - `MetricFieldTests.GeneratesDistinctFields_ForCollidingMetricNames#…`

**Interfaces:**
- Consumes: `MetricFieldTable` from Task 1.
- Produces:
  - `internal static class TaskShapes` with `bool IsAwaitable(ITypeSymbol)`, `bool IsVoidAwaitable(ITypeSymbol)`, `bool IsValueAwaitable(ITypeSymbol)` and `ITypeSymbol UnwrapAwaited(ITypeSymbol)`.
  - `CompileWithGenerator` now also fails on warnings in generated files and on any generator warning or error.

- [ ] **Step 1: Tighten the compile test and add the failing probes**

In `GeneratedCodeCompilesTests.cs`, add a probe type to `ProbeSource`, directly after `public sealed class Inner { ... }`:

```csharp
            public sealed class TaskReport { public int Count { get; set; } }
```

Add these probe members to `ICompileProbe`, after `ConstantAsync`:

```csharp
                // [Count] and [Histogram] without [Trace]: nothing reads the exception, so the
                // catch must not declare it. CS0168 breaks TreatWarningsAsErrors consumers.
                [Count("probe.counted")]
                Task<int> CountOnlyAsync(CancellationToken ct);

                [Histogram("probe.timed")]
                Task TimedOnlyAsync(CancellationToken ct);

                // Synchronous, with a return type whose name merely contains "Task".
                [Count("probe.sync")]
                TaskReport Report();
```

Replace the body of `CompileWithGenerator`, from the `CSharpGeneratorDriver` call to the end, with:

```csharp
        CSharpGeneratorDriver
            .Create(new InstrumentGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var output, out var generatorDiagnostics);

        // Consumers build with TreatWarningsAsErrors, so a warning in the generated proxy or from
        // the generator is a broken build for them, not a cosmetic issue.
        var problems = new List<string>();
        foreach (var d in generatorDiagnostics)
        {
            if (d.Severity >= DiagnosticSeverity.Warning)
                problems.Add(d.ToString());
        }

        foreach (var d in output.GetDiagnostics())
        {
            var inGenerated = d.Location.SourceTree?.FilePath.EndsWith(".g.cs", StringComparison.Ordinal) == true;
            if (d.Severity == DiagnosticSeverity.Error
                || (inGenerated && d.Severity == DiagnosticSeverity.Warning))
            {
                problems.Add(d.ToString());
            }
        }

        return problems.ToArray();
```

In `TraceTests.cs`, add:

```csharp
    /// <summary>
    /// A return type whose name contains "Task" is not a task. Matching the name made the proxy
    /// emit <c>async</c> and <c>await</c> for a synchronous method, which does not compile.
    /// </summary>
    [Fact]
    public void GeneratesSyncProxy_ForReturnTypeNamedLikeTask()
    {
        var source = """
            using ZeroAlloc.Telemetry;

            public sealed class TaskReport { public int Count { get; set; } }

            [Instrument("MyApp")]
            public interface IReportService
            {
                [Count("reports.built")]
                TaskReport Build();
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~GeneratedCodeCompilesTests|FullyQualifiedName~GeneratesSyncProxy"`

Expected: FAIL.
- `GeneratedOutput_Compiles` lists CS0168 `The variable '_ex' is declared but never used` for `CountOnlyAsync` and `TimedOnlyAsync`. It also lists CS1983 on `Report`, because the return type of an async method must be a task-like type.
- `FieldNameProbe_Compiles` lists CS0168.
- `GeneratesSyncProxy_ForReturnTypeNamedLikeTask` reports a missing snapshot.

- [ ] **Step 3: Declare the catch variable only when the span reads it**

In `ProxyWriter.cs`, replace the first line of `WriteCatchBlock`:

```csharp
        // Only the span reads the exception. Declaring the variable otherwise raises CS0168 in
        // the generated file, and that fails every consumer building with TreatWarningsAsErrors.
        sb.AppendLine(method.TraceName is not null
            ? "        catch (Exception _ex)"
            : "        catch (Exception)");
```

- [ ] **Step 4: Accept the changed snapshots and review the diff**

Run: `ZA_SNAPSHOT_UPDATE=1 dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~TraceTests|FullyQualifiedName~TraceTagTests|FullyQualifiedName~MetricFieldTests"`

Then run: `git diff --stat -- tests/ZeroAlloc.Telemetry.Generator.Tests/Snapshots` and `git diff -- tests/ZeroAlloc.Telemetry.Generator.Tests/Snapshots`

Expected: three existing snapshots and the Task 1 snapshot change. Every changed line is `-        catch (Exception _ex)` / `+        catch (Exception)`, and nothing else. A new `GeneratesSyncProxy…` snapshot also appears; it is reviewed in Step 7. Delete any `*.received.cs`.

- [ ] **Step 5: Commit the catch fix**

The async probe still fails at this point. Stage only the catch fix and its snapshots, and leave the new `TraceTests` fact for the next commit:

```bash
git add src/ZeroAlloc.Telemetry.Generator/ProxyWriter.cs \
  "tests/ZeroAlloc.Telemetry.Generator.Tests/Snapshots/TraceTests.GeneratesCounterIncrement_ForCountMethod#OrderServiceInstrumented.g.verified.cs" \
  "tests/ZeroAlloc.Telemetry.Generator.Tests/Snapshots/TraceTests.GeneratesHistogramRecord_ForHistogramMethod#OrderServiceInstrumented.g.verified.cs" \
  "tests/ZeroAlloc.Telemetry.Generator.Tests/Snapshots/TraceTagTests.GeneratesNoTags_WhenMethodHasNoTrace#PlainServiceInstrumented.g.verified.cs" \
  "tests/ZeroAlloc.Telemetry.Generator.Tests/Snapshots/MetricFieldTests.GeneratesDistinctFields_ForCollidingMetricNames#FieldsInstrumented.g.verified.cs"
git commit -F - <<'EOF'
fix(generator): declare the catch variable only when the span reads it

A method with [Count] or [Histogram] but no [Trace] emitted a catch clause whose exception
variable nothing read. That raises CS0168 in the generated file, which fails the build of any
consumer using TreatWarningsAsErrors.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 6: Detect Task and ValueTask by symbol**

Create `src/ZeroAlloc.Telemetry.Generator/TaskShapes.cs`:

```csharp
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// Recognises <c>Task</c>, <c>Task&lt;T&gt;</c>, <c>ValueTask</c> and <c>ValueTask&lt;T&gt;</c>
/// by symbol: the containing namespace plus the metadata name.
/// </summary>
/// <remarks>
/// <para>
/// 1.6.4 searched the return type's display string for <c>"Task"</c>, so a type such as
/// <c>MyTaskResult</c> made the proxy emit <c>async</c> and <c>await</c> for a synchronous method.
/// </para>
/// <para>
/// <c>Compilation.GetTypeByMetadataName</c> is not used: it returns null when more than one
/// referenced assembly defines the type, which a <c>ValueTask</c> polyfill next to the BCL does.
/// Every method would then silently be treated as synchronous.
/// </para>
/// </remarks>
internal static class TaskShapes
{
    private const string TasksNamespace = "System.Threading.Tasks";

    /// <summary>Whether the proxy has to <c>await</c> the inner call.</summary>
    public static bool IsAwaitable(ITypeSymbol type) => IsVoidAwaitable(type) || IsValueAwaitable(type);

    /// <summary><c>Task</c> or <c>ValueTask</c>: awaitable, with no result.</summary>
    public static bool IsVoidAwaitable(ITypeSymbol type) =>
        Is(type, "Task") || Is(type, "ValueTask");

    /// <summary><c>Task&lt;T&gt;</c> or <c>ValueTask&lt;T&gt;</c>.</summary>
    public static bool IsValueAwaitable(ITypeSymbol type) =>
        Is(type, "Task`1") || Is(type, "ValueTask`1");

    /// <summary>The awaited type for <c>Task&lt;T&gt;</c>/<c>ValueTask&lt;T&gt;</c>, otherwise the type itself.</summary>
    public static ITypeSymbol UnwrapAwaited(ITypeSymbol type) =>
        IsValueAwaitable(type) && type is INamedTypeSymbol { TypeArguments.Length: 1 } named
            ? named.TypeArguments[0]
            : type;

    private static bool Is(ITypeSymbol type, string metadataName) =>
        type is INamedTypeSymbol named
        && string.Equals(named.OriginalDefinition.MetadataName, metadataName, StringComparison.Ordinal)
        && named.ContainingNamespace is { } ns
        && string.Equals(ns.ToDisplayString(), TasksNamespace, StringComparison.Ordinal);
}
```

In `InstrumentGenerator.cs`, `BuildMethods`, replace lines 192-196:

```csharp
            var isAsync     = TaskShapes.IsAwaitable(member.ReturnType);
            var returnsVoid = member.ReturnsVoid || TaskShapes.IsVoidAwaitable(member.ReturnType);
```

Replace the body of `ResultCanBeNull`, keeping its doc comment:

```csharp
    private static bool ResultCanBeNull(IMethodSymbol method) =>
        CanBeNull(TaskShapes.UnwrapAwaited(method.ReturnType));
```

Delete `UnwrapAwaited`, at lines 330-344. In `BuildResultTags`, replace `UnwrapAwaited(method.ReturnType)` with `TaskShapes.UnwrapAwaited(method.ReturnType)`.

- [ ] **Step 7: Accept the sync snapshot and run the suite**

Run: `ZA_SNAPSHOT_UPDATE=1 dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~GeneratesSyncProxy"`

The snapshot `TraceTests.GeneratesSyncProxy_ForReturnTypeNamedLikeTask#ReportServiceInstrumented.g.verified.cs` must read exactly:

```csharp
//HintName: ReportServiceInstrumented.g.cs
// <auto-generated />
#pragma warning disable EPC12 // catch reads _ex.Message for span status, then rethrows
#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

internal sealed class ReportServiceInstrumented : IReportService
{
    private static readonly ActivitySource _activitySource = new("MyApp");
    private static readonly Meter _meter = new("MyApp");
    private static readonly Counter<long> _reports_built = _meter.CreateCounter<long>("reports.built");

    private readonly IReportService _inner;
    public ReportServiceInstrumented(IReportService inner) => _inner = inner;

    public global::TaskReport Build()
    {
        try
        {
            var _result = _inner.Build();
            _reports_built.Add(1);
            return _result;
        }
        catch (Exception)
        {
            throw;
        }
    }
}
```

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj`

Expected: `Passed!  - Failed:     0`.

If `GeneratedOutput_Compiles` lists any other warning in a `.g.cs` file, it is a real consumer-facing defect. Fix the emitted code in this task, and do not relax the filter.

- [ ] **Step 8: Commit the async fix**

```bash
git add src/ZeroAlloc.Telemetry.Generator tests/ZeroAlloc.Telemetry.Generator.Tests
git commit -F - <<'EOF'
fix(generator): detect Task and ValueTask returns by symbol

Async detection searched the return type's name for "Task", so a type such as MyTaskResult
made the proxy emit async and await for a synchronous method, which does not compile. Task,
Task<T>, ValueTask and ValueTask<T> are now matched on the symbol's namespace and metadata name.

The compile test now also fails on warnings in generated files and on generator diagnostics,
since a warning in the proxy is a broken build for TreatWarningsAsErrors consumers.

Refs #142

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 3: Shared path resolver, ZTEL007, ZTEL008, and ZTEL005 naming the attribute

Two commits. A `fix:` for the nullable `Value` segment comes first, then the `feat:` for the diagnostics.

**Files:**
- Create: `src/ZeroAlloc.Telemetry.Generator/PathResolver.cs`
- Create: `src/ZeroAlloc.Telemetry.Generator/AttributeLocations.cs`
- Modify: `src/ZeroAlloc.Telemetry.Generator/InstrumentGenerator.cs`:
  - `BuildMethods`;
  - `CanBeNull`, `UnwrapNullable`, `ResolveMemberAccess` ×2 and `FindMemberType` (346-454), all moved out;
  - `ReportTagDiagnostics` (460-504), `BuildParameters` (506-537), `BuildResultTags` (562-602) and `BuildGuardExpression` (657-671).
- Modify: `src/ZeroAlloc.Telemetry.Generator/ProxyWriter.cs`, `WriteResultTags` (233-282)
- Modify: `src/ZeroAlloc.Telemetry.Generator/InstrumentDiagnostics.cs`
- Modify: `src/ZeroAlloc.Telemetry.Generator/AnalyzerReleases.Unshipped.md`
- Modify: `tests/ZeroAlloc.Telemetry.Generator.Tests/DiagnosticTests.cs`, `GeneratedCodeCompilesTests.cs` and `TraceTagTests.cs`
- Delete: `tests/ZeroAlloc.Telemetry.Generator.Tests/Snapshots/TraceTagTests.GeneratesNoTags_WhenMethodHasNoTrace.verified.txt`. It is a leftover from Verify: `GeneratorSnapshot.Verify` only reads and writes `.verified.cs`, so nothing reads this file.

**Interfaces:**
- Consumes: `TaskShapes.UnwrapAwaited` from Task 2.
- Produces:
  - `PathResolver.Resolve(ITypeSymbol rootType, string path) -> PathResolver.Resolution`
  - `PathResolver.Resolution` with `string? Access`, `ITypeSymbol? FinalType`, `bool CanBeNull`, `string? MissingSegment`, `ITypeSymbol? MissingOn` and `bool Resolved`.
  - `PathResolver.CanBeNull(ITypeSymbol)`, `PathResolver.UnwrapNullable(ITypeSymbol)`, `PathResolver.IsBoolean(ITypeSymbol)`.
  - `AttributeLocations.Positional(AttributeData, int index, string parameterName, Location fallback)` and `AttributeLocations.Named(AttributeData, string propertyName, Location fallback)`.
  - `InstrumentDiagnostics.ResultReadOnVoidMethod`, the renamed ZTEL005 with `{0}` = attribute, `{1}` = type and `{2}` = method.
  - `InstrumentDiagnostics.MemberPathNotFound` (ZTEL007) and `InstrumentDiagnostics.WhenNotBoolean` (ZTEL008).
  - Generator helpers: `bool TryBuildGuard(AttributeData attr, IMethodSymbol method, ITypeSymbol resultType, string? when, ImmutableArray<Diagnostic>.Builder diagnostics, out string? guard)`, `Diagnostic PathNotFound(Location location, string path, PathResolver.Resolution resolution)`, `bool IsAttribute(AttributeData, string fqn)`, `string? GetNamedString(AttributeData, string name)` and `Location MethodLocation(IMethodSymbol)`.

#### Part A: nullable `Value` segment (`fix:`)

- [ ] **Step 1: Write the failing compile probe and snapshot test**

In `GeneratedCodeCompilesTests.ProbeSource`, add to `ICompileProbe`:

```csharp
                // "Value" on a nullable value type is dropped because ?. already unwraps it. The
                // next segment must then stay null-safe, and a guard ending there compares to true.
                [Trace("probe.nullableStruct")]
                [TraceTagFromResult("ns.width", "Value.Width")]
                Task<Extent?> NullableStructAsync(CancellationToken ct);

                [Trace("probe.nullableBoolGuard")]
                [TraceTagFromResult("nb.flag", When = "Value")]
                Task<bool?> NullableBoolGuardAsync(CancellationToken ct);
```

In `TraceTagTests.cs`, add:

```csharp
    /// <summary>
    /// A <c>Value</c> segment on a nullable value type is dropped, because <c>?.</c> already
    /// unwraps it. 1.6.4 then emitted a plain <c>.</c> for the next segment, and a bare
    /// <c>bool?</c> as a guard. Neither compiles.
    /// </summary>
    [Fact]
    public void GeneratesNullSafeAccess_AfterValueOnNullableValueType()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            public readonly struct Extent { public int Width { get; } }

            [Instrument("MyApp.Extents")]
            public interface IExtents
            {
                [Trace("extents.width")]
                [TraceTagFromResult("extent.width", "Value.Width")]
                Task<Extent?> WidthAsync(CancellationToken ct);

                [Trace("extents.flag")]
                [TraceTagFromResult("extent.flag", When = "Value")]
                Task<bool?> FlagAsync(CancellationToken ct);
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }
```

- [ ] **Step 2: Run to verify failure**

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~GeneratedOutput_Compiles|FullyQualifiedName~AfterValueOnNullableValueType"`

Expected: FAIL.
- The compile test lists CS1061 `'Extent?' does not contain a definition for 'Width'` for `_tagged.Width`.
- It also lists CS0266 `Cannot implicitly convert type 'bool?' to 'bool'` for `if (_tagged)`.
- The snapshot test reports a missing snapshot.

- [ ] **Step 3: Move the resolver into `PathResolver` and fix the `Value` step**

Create `src/ZeroAlloc.Telemetry.Generator/PathResolver.cs`:

```csharp
using System.Text;
using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// Resolves the dotted member paths used by <c>[TraceTag]</c>, <c>[TraceTagFromResult]</c>,
/// <c>[CountFromResult]</c>, <c>[HistogramFromResult]</c> and every <c>When</c> guard. One
/// resolver gives one set of nullable rules and one meaning of a path.
/// </summary>
/// <remarks>
/// The operator for each segment cannot be chosen from the path text: <c>?.</c> is required
/// wherever the preceding value may be null, and is a compile error wherever it cannot be. So
/// each segment is resolved to a symbol and the operator is picked from the type before it.
/// </remarks>
internal static class PathResolver
{
    /// <summary>The outcome of resolving one path.</summary>
    internal sealed class Resolution
    {
        public Resolution(string? access, ITypeSymbol? finalType, bool canBeNull, string? missingSegment, ITypeSymbol? missingOn)
        {
            Access = access;
            FinalType = finalType;
            CanBeNull = canBeNull;
            MissingSegment = missingSegment;
            MissingOn = missingOn;
        }

        /// <summary>
        /// The access to append to the root, for example <c>?.Value?.Count</c>. Empty means the
        /// root itself. Null when the path does not resolve.
        /// </summary>
        public string? Access { get; }

        /// <summary>
        /// The type the path ends at. A trailing <c>Value</c> on a nullable value type ends at
        /// the underlying type, with <see cref="CanBeNull"/> set.
        /// </summary>
        public ITypeSymbol? FinalType { get; }

        /// <summary>Whether the emitted expression can evaluate to null.</summary>
        public bool CanBeNull { get; }

        /// <summary>The first segment that names nothing. Empty for an empty segment, as in <c>A..B</c>.</summary>
        public string? MissingSegment { get; }

        /// <summary>The type <see cref="MissingSegment"/> was looked up on.</summary>
        public ITypeSymbol? MissingOn { get; }

        public bool Resolved => Access is not null;
    }

    /// <summary>
    /// Resolves <paramref name="path"/> against <paramref name="rootType"/>. An empty or
    /// whitespace path resolves to the root itself.
    /// </summary>
    public static Resolution Resolve(ITypeSymbol rootType, string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return new Resolution(string.Empty, rootType, CanBeNull(rootType), null, null);

        var current = rootType;
        var sb = new StringBuilder();

        // Set after a Value segment on a Nullable<T> has been dropped. The expression is still
        // nullable, but current is already T, so the next segment is looked up on T and needs ?.
        var unwrapped = false;

        foreach (var rawSegment in path.Split('.'))
        {
            var segment = rawSegment.Trim();
            var nullable = unwrapped || CanBeNull(current);
            var underlying = UnwrapNullable(current);

            // `x?.Value` on a Nullable<T> does not mean Nullable<T>.Value: the null-conditional
            // unwraps first, so the member is looked up on T and `.Value` fails to compile. The
            // segment is also redundant. So drop it and carry on from T. The expression stays
            // nullable, and 1.6.4 lost exactly that: the next segment got `.` against a
            // Nullable<T>, and a guard ending here was emitted as a bare bool?.
            if (!unwrapped && IsNullableValueType(current)
                && string.Equals(segment, "Value", StringComparison.Ordinal))
            {
                current = underlying;
                unwrapped = true;
                continue;
            }

            var memberType = segment.Length == 0 ? null : FindMemberType(underlying, segment);
            if (memberType is null)
                return new Resolution(null, null, false, segment, underlying);

            sb.Append(nullable ? "?." : ".").Append(segment);
            current = memberType;
            unwrapped = false;
        }

        // May be empty when every segment resolved away, as with a bare "Value" on a nullable
        // result. That is a successful resolution meaning "the root itself".
        var access = sb.ToString();
        var canBeNull = unwrapped
            || access.IndexOf("?.", StringComparison.Ordinal) >= 0
            || CanBeNull(current);

        return new Resolution(access, current, canBeNull, null, null);
    }

    /// <summary>A reference type or <c>Nullable&lt;T&gt;</c>.</summary>
    public static bool CanBeNull(ITypeSymbol type) =>
        type.IsReferenceType || IsNullableValueType(type);

    /// <summary>Returns T for <c>Nullable&lt;T&gt;</c>, otherwise the type unchanged.</summary>
    public static ITypeSymbol UnwrapNullable(ITypeSymbol type) =>
        IsNullableValueType(type) && type is INamedTypeSymbol { TypeArguments.Length: 1 } n
            ? n.TypeArguments[0]
            : type;

    /// <summary><c>bool</c> or <c>bool?</c>.</summary>
    public static bool IsBoolean(ITypeSymbol type) =>
        UnwrapNullable(type).SpecialType == SpecialType.System_Boolean;

    private static bool IsNullableValueType(ITypeSymbol type) =>
        type.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T;

    /// <summary>Finds a property or field by name, walking base types and then interfaces.</summary>
    private static ITypeSymbol? FindMemberType(ITypeSymbol type, string name)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            foreach (var m in t.GetMembers(name))
            {
                if (m is IPropertySymbol { Parameters.Length: 0 } p) return p.Type;
                if (m is IFieldSymbol f) return f.Type;
            }
        }

        // Interfaces do not inherit through BaseType, so check the full interface set too.
        // ICollection<T>.Count on an IReadOnlyList<T> is the common case.
        foreach (var iface in type.AllInterfaces)
        {
            foreach (var m in iface.GetMembers(name))
            {
                if (m is IPropertySymbol { Parameters.Length: 0 } p) return p.Type;
            }
        }

        return null;
    }
}
```

In `InstrumentGenerator.cs`, delete `CanBeNull`, `UnwrapNullable`, both `ResolveMemberAccess` overloads and `FindMemberType`, at lines 346-454.

Replace `ResultCanBeNull`:

```csharp
    private static bool ResultCanBeNull(IMethodSymbol method) =>
        PathResolver.CanBeNull(TaskShapes.UnwrapAwaited(method.ReturnType));
```

In `BuildParameters`, replace the body of `if (tagName is not null && !string.IsNullOrEmpty(member))`. It keeps today's behaviour for an unresolved parameter path; see Design decision 7.

```csharp
                accessSuffix = PathResolver.Resolve(ps[i].Type, member!).Access;

                // A copy is only needed when the emitted access actually null-tests the
                // argument; a plain `.Member` on a non-nullable value leaves its state alone.
                needsCopy = accessSuffix is null
                    ? PathResolver.CanBeNull(ps[i].Type)
                    : accessSuffix.StartsWith("?.", StringComparison.Ordinal);
```

In `BuildResultTags`, replace `ResolveMemberAccess(resultType, member!)` with `PathResolver.Resolve(resultType, member!).Access`.

Replace `BuildGuardExpression`, keeping its doc comment:

```csharp
    private static string? BuildGuardExpression(ITypeSymbol resultType, string? when)
    {
        if (string.IsNullOrWhiteSpace(when))
            return null;

        var path = PathResolver.Resolve(resultType, when!);
        if (!path.Resolved)
        {
            // Unresolvable: emit as written and let the compiler name the bad member. Part B
            // replaces this with ZTEL007.
            return PathResolver.CanBeNull(resultType) ? $"?.{when} == true" : $".{when} == true";
        }

        return path.CanBeNull ? path.Access + " == true" : path.Access;
    }
```

- [ ] **Step 4: Accept the snapshot and run the suite**

Run: `ZA_SNAPSHOT_UPDATE=1 dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~AfterValueOnNullableValueType"`

The method bodies in `TraceTagTests.GeneratesNullSafeAccess_AfterValueOnNullableValueType#ExtentsInstrumented.g.verified.cs` must read:

```csharp
    public async global::System.Threading.Tasks.Task<global::Extent?> WidthAsync(global::System.Threading.CancellationToken ct)
    {
        using var _activity = _activitySource.StartActivity("extents.width");
        try
        {
            var _result = await _inner.WidthAsync(ct);
            var _tagged = _result;
            _activity?.SetTag("extent.width", _tagged?.Width);
            return _result;
        }
        catch (Exception _ex)
        {
            _activity?.SetStatus(ActivityStatusCode.Error, _ex.Message);
            throw;
        }
    }

    public async global::System.Threading.Tasks.Task<bool?> FlagAsync(global::System.Threading.CancellationToken ct)
    {
        using var _activity = _activitySource.StartActivity("extents.flag");
        try
        {
            var _result = await _inner.FlagAsync(ct);
            var _tagged = _result;
            if (_tagged == true)
                _activity?.SetTag("extent.flag", _result);
            return _result;
        }
        catch (Exception _ex)
        {
            _activity?.SetStatus(ActivityStatusCode.Error, _ex.Message);
            throw;
        }
    }
```

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj`

Expected: `Passed!  - Failed:     0`, with no existing snapshot changed. Check with `git status --short tests/`.

- [ ] **Step 5: Commit**

```bash
git add src/ZeroAlloc.Telemetry.Generator tests/ZeroAlloc.Telemetry.Generator.Tests
git commit -F - <<'EOF'
fix(generator): keep null-safe access after a Value segment on a nullable value type

A Value segment on a Nullable<T> is dropped, since ?. already unwraps it. The resolver then
treated the rest of the path as non-nullable: Value.Width on a Task<Extent?> emitted a plain
member access on the Nullable, and When = "Value" on a Task<bool?> emitted a bare bool? as the
condition. Neither compiled.

The resolver moves to its own PathResolver, which the new metric attributes will share.

Refs #142

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

#### Part B: ZTEL007, ZTEL008, and ZTEL005 naming the attribute (`feat:`)

- [ ] **Step 6: Write the failing diagnostic tests**

In `DiagnosticTests.cs`, add `using System.Globalization;`, then add these helpers next to `RunAndCollectDiagnostics`:

```csharp
    private static Diagnostic Single(Diagnostic[] diagnostics, string id) =>
        Assert.Single(diagnostics, d => string.Equals(d.Id, id, StringComparison.Ordinal));

    /// <summary>The source text a diagnostic points at, so tests assert the exact location.</summary>
    private static string LocationText(Diagnostic d) =>
        d.Location.SourceTree!.GetText().ToString(d.Location.SourceSpan);

    private const string PathNotFoundTail =
        "Each segment of a member path names a property or field of the type reached so far, starting from the awaited return value.";

    private static string TotalsSource(string attributes) => $$"""
        using ZeroAlloc.Telemetry;
        using System.Threading.Tasks;

        public sealed class Totals
        {
            public int Total { get; set; }
            public int Count { get; set; }
            public bool? MaybeOk { get; set; }
        }

        [Instrument("MyApp")]
        public interface ITotals
        {
            [Trace("totals.get")]
            {{attributes}}
            Task<Totals> GetAsync();
        }
        """;
```

Replace `ZTEL005_ResultTagOnVoidReturningMethod_ProducesWarning` with:

```csharp
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
```

Add:

```csharp
    [Theory]
    [InlineData("[TraceTagFromResult(\"t\", \"Totl\")]")]
    [InlineData("[TraceTagFromResult(\"t\", \"Total\", When = \"Totl\")]")]
    public void ZTEL007_UnresolvedSegment_IsReportedAtTheArgument(string attributes)
    {
        var diagnostics = RunAndCollectDiagnostics(TotalsSource(attributes));

        var d = Single(diagnostics, "ZTEL007");
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal("\"Totl\"", LocationText(d));
        Assert.Equal(
            "'Totl' in the path 'Totl' is not a property or field of 'Totals'. " + PathNotFoundTail,
            d.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ZTEL007_NamesTheSegmentAndTheTypeItWasLookedUpOn()
    {
        var diagnostics = RunAndCollectDiagnostics(TotalsSource("[TraceTagFromResult(\"t\", \"Total.Digits\")]"));

        var d = Single(diagnostics, "ZTEL007");
        Assert.Equal("\"Total.Digits\"", LocationText(d));
        Assert.Equal(
            "'Digits' in the path 'Total.Digits' is not a property or field of 'int'. " + PathNotFoundTail,
            d.GetMessage(CultureInfo.InvariantCulture));
    }

    [Theory]
    [InlineData("[TraceTagFromResult(\"t\", \"Total\", When = \"Count\")]")]
    public void ZTEL008_NonBooleanWhen_ProducesError(string attributes)
    {
        var diagnostics = RunAndCollectDiagnostics(TotalsSource(attributes));

        var d = Single(diagnostics, "ZTEL008");
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal("\"Count\"", LocationText(d));
        Assert.Equal(
            "When = 'Count' resolves to 'int'. A guard must name a member of type bool or bool? on the awaited return value, such as IsSuccess.",
            d.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ZTEL008_NullableBoolWhen_ProducesNoError()
    {
        var diagnostics = RunAndCollectDiagnostics(TotalsSource("[TraceTagFromResult(\"t\", \"Total\", When = \"MaybeOk\")]"));

        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZTEL007", StringComparison.Ordinal));
        Assert.DoesNotContain(diagnostics, d => string.Equals(d.Id, "ZTEL008", StringComparison.Ordinal));
    }
```

- [ ] **Step 7: Run to verify failure**

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~DiagnosticTests"`

Expected: FAIL.
- The ZTEL007 and ZTEL008 tests fail with `Assert.Single() Failure: The collection was empty`.
- The ZTEL005 test fails on the message, because the current text starts `[TraceTagFromResult] on 'IOrderService.CreateAsync' records nothing — the method returns void, Task or ValueTask, so there is no result to read. Remove the attribute or return a value.`.

- [ ] **Step 8: Add the descriptors and release rows**

In `InstrumentDiagnostics.cs`, replace `ResultTagOnVoidMethod` with:

```csharp
    public static readonly DiagnosticDescriptor ResultReadOnVoidMethod = new(
        id: "ZTEL005",
        title: "Result-reading attribute on a method with no return value",
        messageFormat: "[{0}] on '{1}.{2}' records nothing — the method returns void, Task or ValueTask, so there is no result to read. Remove it or return a value.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
```

Add after `UnknownSpanNameToken`:

```csharp
    public static readonly DiagnosticDescriptor MemberPathNotFound = new(
        id: "ZTEL007",
        title: "Member path does not resolve",
        messageFormat: "'{0}' in the path '{1}' is not a property or field of '{2}'. Each segment of a member path names a property or field of the type reached so far, starting from the awaited return value.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor WhenNotBoolean = new(
        id: "ZTEL008",
        title: "When must name a bool member",
        messageFormat: "When = '{0}' resolves to '{1}'. A guard must name a member of type bool or bool? on the awaited return value, such as IsSuccess.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
```

Replace `src/ZeroAlloc.Telemetry.Generator/AnalyzerReleases.Unshipped.md` with:

```markdown
; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category            | Severity | Notes
--------|---------------------|----------|---------------------------------------------------------------------------
ZTEL007 | ZeroAlloc.Telemetry | Error    | A segment of a member path or When guard names no property or field
ZTEL008 | ZeroAlloc.Telemetry | Error    | A When guard resolves to a member that is not bool or bool?
```

ZTEL005 keeps its category and severity. Release tracking records only those, so its reworded message gets no row; see Design decision 12.

- [ ] **Step 9: Add `AttributeLocations`**

Create `src/ZeroAlloc.Telemetry.Generator/AttributeLocations.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// Finds where an attribute argument is written, so a diagnostic about a path points at the
/// path rather than at the method.
/// </summary>
internal static class AttributeLocations
{
    /// <summary>The expression of positional argument <paramref name="index"/>, or of the argument named with <c>parameterName:</c>.</summary>
    public static Location Positional(AttributeData attribute, int index, string parameterName, Location fallback)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax syntax)
            return fallback;

        if (syntax.ArgumentList is null)
            return syntax.GetLocation();

        var position = 0;
        foreach (var argument in syntax.ArgumentList.Arguments)
        {
            // Property assignments such as When = "..." follow the constructor arguments.
            if (argument.NameEquals is not null)
                continue;

            if (argument.NameColon is not null)
            {
                if (string.Equals(argument.NameColon.Name.Identifier.ValueText, parameterName, StringComparison.Ordinal))
                    return argument.Expression.GetLocation();
            }
            else if (position == index)
            {
                return argument.Expression.GetLocation();
            }

            position++;
        }

        return syntax.GetLocation();
    }

    /// <summary>The expression assigned to <paramref name="propertyName"/>, as in <c>When = "IsSuccess"</c>.</summary>
    public static Location Named(AttributeData attribute, string propertyName, Location fallback)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax() is not AttributeSyntax syntax)
            return fallback;

        if (syntax.ArgumentList is null)
            return syntax.GetLocation();

        foreach (var argument in syntax.ArgumentList.Arguments)
        {
            if (argument.NameEquals is { } nameEquals
                && string.Equals(nameEquals.Name.Identifier.ValueText, propertyName, StringComparison.Ordinal))
            {
                return argument.Expression.GetLocation();
            }
        }

        return syntax.GetLocation();
    }
}
```

- [ ] **Step 10: Validate paths and guards in the generator**

In `InstrumentGenerator.cs`, `BuildMethods`, replace `var resultTags = BuildResultTags(member);` with:

```csharp
            var resultType = TaskShapes.UnwrapAwaited(member.ReturnType);
            var resultTags = BuildResultTags(member, resultType, returnsVoid, diagnostics);
```

In `ReportTagDiagnostics`, replace the ZTEL005 block at lines 497-503:

```csharp
        // Reported independently of [Trace]: the attribute is wrong on a void method either way.
        if (returnsVoid && resultTags.Count > 0)
        {
            diagnostics.Add(Diagnostic.Create(
                InstrumentDiagnostics.ResultReadOnVoidMethod,
                location, "TraceTagFromResult", target.ToDisplayString(), member.Name));
        }
```

Replace `BuildResultTags`:

```csharp
    private static ResultTagModel[] BuildResultTags(
        IMethodSymbol method,
        ITypeSymbol resultType,
        bool returnsVoid,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        List<ResultTagModel>? tags = null;
        foreach (var attr in method.GetAttributes())
        {
            if (!IsAttribute(attr, TraceTagFromResultAttrFqn))
                continue;

            if (attr.ConstructorArguments.Length == 0)
                continue;

            var name = attr.ConstructorArguments[0].Value as string;
            if (string.IsNullOrEmpty(name))
                continue;

            // Second positional argument is the optional member path.
            var member = attr.ConstructorArguments.Length > 1
                ? attr.ConstructorArguments[1].Value as string
                : null;

            // No result to resolve against. ZTEL005 reports the attribute and the writer emits
            // nothing for it, so resolving against Task would only add a misleading ZTEL007.
            if (returnsVoid)
            {
                (tags ??= new List<ResultTagModel>()).Add(new ResultTagModel(name!, member));
                continue;
            }

            string? accessSuffix = null;
            if (!string.IsNullOrEmpty(member))
            {
                var path = PathResolver.Resolve(resultType, member!);
                if (!path.Resolved)
                {
                    diagnostics.Add(PathNotFound(
                        AttributeLocations.Positional(attr, 1, "member", MethodLocation(method)), member!, path));
                    continue;
                }

                accessSuffix = path.Access;
            }

            if (!TryBuildGuard(attr, method, resultType, GetNamedString(attr, "When"), diagnostics, out var guard))
                continue;

            (tags ??= new List<ResultTagModel>()).Add(new ResultTagModel(name!, member, accessSuffix, guard));
        }

        return tags?.ToArray() ?? [];
    }
```

Replace `BuildGuardExpression` with `TryBuildGuard` and the helpers:

```csharp
    /// <summary>
    /// Resolves a <c>When</c> guard into the condition appended to the result root. Returns false,
    /// having reported ZTEL007 or ZTEL008, when the guard cannot be emitted. The caller then emits
    /// nothing for the attribute, so the diagnostic is the only error the user sees.
    /// </summary>
    /// <remarks>
    /// The comparison against true is added only when the guard can be null, either because a
    /// step along the path is null-tested or because the member is <c>bool?</c>. On a plain bool
    /// the bare expression is emitted, since <c>x == true</c> reads as noise.
    /// </remarks>
    private static bool TryBuildGuard(
        AttributeData attr,
        IMethodSymbol method,
        ITypeSymbol resultType,
        string? when,
        ImmutableArray<Diagnostic>.Builder diagnostics,
        out string? guard)
    {
        guard = null;
        if (string.IsNullOrWhiteSpace(when))
            return true;

        var location = AttributeLocations.Named(attr, "When", MethodLocation(method));
        var path = PathResolver.Resolve(resultType, when!);
        if (!path.Resolved)
        {
            diagnostics.Add(PathNotFound(location, when!, path));
            return false;
        }

        if (!PathResolver.IsBoolean(path.FinalType!))
        {
            diagnostics.Add(Diagnostic.Create(
                InstrumentDiagnostics.WhenNotBoolean, location, when, path.FinalType!.ToDisplayString()));
            return false;
        }

        guard = path.CanBeNull ? path.Access + " == true" : path.Access;
        return true;
    }

    private static Diagnostic PathNotFound(Location location, string path, PathResolver.Resolution resolution) =>
        Diagnostic.Create(
            InstrumentDiagnostics.MemberPathNotFound,
            location,
            resolution.MissingSegment,
            path,
            resolution.MissingOn?.ToDisplayString());

    private static bool IsAttribute(AttributeData attr, string attributeFqn) =>
        string.Equals(attr.AttributeClass?.ToDisplayString(), attributeFqn, StringComparison.Ordinal);

    private static string? GetNamedString(AttributeData attr, string name)
    {
        foreach (var named in attr.NamedArguments)
        {
            if (string.Equals(named.Key, name, StringComparison.Ordinal))
                return named.Value.Value as string;
        }

        return null;
    }

    private static Location MethodLocation(IMethodSymbol method) =>
        method.Locations.FirstOrDefault() ?? Location.None;
```

In `ProxyWriter.WriteResultTags`, replace the three-branch `access` computation. A tag with a member now always carries a resolved suffix, because the generator drops the tag and reports ZTEL007 otherwise:

```csharp
        var root = method.ResultCanBeNull ? "_tagged" : "_result";

        foreach (var tag in method.ResultTags)
        {
            // No member records the result itself: no member access, so no null test and no
            // effect on _result's null-state. Otherwise the generator resolved the path, choosing
            // the operator for every segment, and reported ZTEL007 instead where it could not.
            var access = string.IsNullOrEmpty(tag.Member) ? "_result" : root + tag.AccessSuffix;

            if (tag.GuardExpression is { } guard)
            {
                // The guard has to prevent the member being read at all, which a null-conditional
                // cannot: `?.` protects against a null result, not against a result whose value is
                // unset. Reading Value on a failed Result is meaningless at best and throws at worst.
                sb.AppendLine($"            if ({root}{guard})");
                sb.AppendLine($"                _activity?.SetTag({Literal(tag.TagName)}, {access});");
            }
            else
            {
                sb.AppendLine($"            _activity?.SetTag({Literal(tag.TagName)}, {access});");
            }
        }
```

Delete the stale Verify leftover:

```bash
git rm "tests/ZeroAlloc.Telemetry.Generator.Tests/Snapshots/TraceTagTests.GeneratesNoTags_WhenMethodHasNoTrace.verified.txt"
```

- [ ] **Step 11: Run the suite**

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj`

Expected: `Passed!  - Failed:     0`. No snapshot changes, which `git status --short tests/` confirms: only `DiagnosticTests.cs` and the deleted `.txt` show.

- [ ] **Step 12: Commit**

```bash
git add src/ZeroAlloc.Telemetry.Generator tests/ZeroAlloc.Telemetry.Generator.Tests
git commit -F - <<'EOF'
feat(generator): report unresolved member paths and non-bool When guards

A misspelt [TraceTagFromResult] member or When guard used to surface as a compile error inside
the generated proxy. ZTEL007 now names the segment and the type it was looked up on, and ZTEL008
reports a When that resolves to neither bool nor bool?. Both are errors at the attribute
argument, and the tag is not emitted, so the diagnostic is the only error.

ZTEL005 now names the attribute it reports, ready for the metric attributes that follow.

Refs #142

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 4: New attributes, `Unit`/`Description`/`When` properties, PublicAPI, ZTEL003 registrations

**Files:**
- Modify: `src/ZeroAlloc.Telemetry/CountAttribute.cs`
- Modify: `src/ZeroAlloc.Telemetry/HistogramAttribute.cs`
- Create: `src/ZeroAlloc.Telemetry/CountFromResultAttribute.cs`
- Create: `src/ZeroAlloc.Telemetry/HistogramFromResultAttribute.cs`
- Modify: `src/ZeroAlloc.Telemetry/PublicAPI.Unshipped.txt`
- Modify: `src/ZeroAlloc.Telemetry.Generator/InstrumentGenerator.cs`: constants (13-19), `Initialize` (83-85), `BuildMethods`
- Modify: `src/ZeroAlloc.Telemetry.Generator/Models/MetricModel.cs`
- Modify: `src/ZeroAlloc.Telemetry.Generator/MetricFieldTable.cs`
- Modify: `src/ZeroAlloc.Telemetry.Generator/ProxyWriter.cs`, `WriteMetricFields`
- Modify: `src/ZeroAlloc.Telemetry.Generator/InstrumentDiagnostics.cs`, the ZTEL003 title
- Modify: `tests/ZeroAlloc.Telemetry.Generator.Tests/MetricFieldTests.cs` and `DiagnosticTests.cs`

**Interfaces:**
- Consumes: `IsAttribute`, `GetNamedString` and `MethodLocation` from Task 3.
- Produces:
  - Public attributes `ZeroAlloc.Telemetry.CountFromResultAttribute(string metric, string member)` and `ZeroAlloc.Telemetry.HistogramFromResultAttribute(string metric, string member)`. Each has `Metric`, `Member`, `When`, `Unit` and `Description`.
  - `CountAttribute` and `HistogramAttribute` gain `When`, `Unit` and `Description`.
  - `MetricModel(MetricKind Kind, string Metric, string? Unit = null, string? Description = null)`.
  - `MetricFieldTable.Field(MetricKind Kind, string Metric, string FieldName, string? Unit, string? Description)`.
  - Generator constants `CountFromResultAttrFqn` and `HistogramFromResultAttrFqn`, and `AttributeData? FindAttribute(IMethodSymbol, string fqn)`.

In this task the generator reads `Unit` and `Description` on `[Count]`/`[Histogram]`. `When` and the two new attributes are emitted in Task 5.

- [ ] **Step 1: Add the properties and attributes, without PublicAPI lines**

Replace `src/ZeroAlloc.Telemetry/CountAttribute.cs`:

```csharp
namespace ZeroAlloc.Telemetry;

/// <summary>
/// Increments a <see cref="System.Diagnostics.Metrics.Counter{T}"/> of <c>long</c>
/// by 1 after a successful (non-throwing) call.
/// </summary>
/// <remarks>
/// <para>
/// A method returning a <c>Result&lt;T, E&gt;</c> returns normally when it fails, so without a
/// guard a failed Result is counted as a success. Set <see cref="When"/> to count only the calls
/// whose result says they succeeded.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [Count("orders.accepted", When = "IsSuccess", Unit = "{order}")]
/// ValueTask&lt;Result&lt;OrderId, OrderError&gt;&gt; AcceptAsync(Order order, CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method)]
public sealed class CountAttribute : Attribute
{
    /// <summary>The metric name passed to <see cref="System.Diagnostics.Metrics.Meter.CreateCounter{T}(string, string?, string?)"/>.</summary>
    public string Metric { get; }

    /// <summary>
    /// A dotted path from the awaited return value to a <c>bool</c> or <c>bool?</c> member that
    /// must be true for the call to be counted, e.g. <c>IsSuccess</c>. Null counts every
    /// non-throwing call.
    /// </summary>
    /// <remarks>
    /// Same semantics as <see cref="TraceTagFromResultAttribute.When"/>. The method must return a
    /// value; on <c>void</c>, <c>Task</c> or <c>ValueTask</c> the generator reports <c>ZTEL005</c>
    /// and the counter records nothing.
    /// </remarks>
    public string? When { get; set; }

    /// <summary>The instrument's unit, e.g. <c>{order}</c>. Null passes no unit.</summary>
    public string? Unit { get; set; }

    /// <summary>The instrument's description. Null passes none.</summary>
    public string? Description { get; set; }

    /// <summary>Counts calls under <paramref name="metric"/>.</summary>
    /// <param name="metric">The metric name.</param>
    public CountAttribute(string metric) => Metric = metric;
}
```

Replace `src/ZeroAlloc.Telemetry/HistogramAttribute.cs`:

```csharp
namespace ZeroAlloc.Telemetry;

/// <summary>
/// Records the elapsed time in milliseconds in a
/// <see cref="System.Diagnostics.Metrics.Histogram{T}"/> of <c>double</c>
/// after each call (successful or failed).
/// </summary>
/// <remarks>
/// <para>
/// With <see cref="When"/> set, only non-throwing calls whose guard is true are recorded. A call
/// that throws has no result to evaluate the guard against, so a guarded histogram records
/// nothing for it. An unguarded histogram still records on both paths.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method)]
public sealed class HistogramAttribute : Attribute
{
    /// <summary>The metric name passed to <see cref="System.Diagnostics.Metrics.Meter.CreateHistogram{T}(string, string?, string?)"/>.</summary>
    public string Metric { get; }

    /// <summary>
    /// A dotted path from the awaited return value to a <c>bool</c> or <c>bool?</c> member that
    /// must be true for the duration to be recorded, e.g. <c>IsSuccess</c>. Null records every
    /// call, including calls that throw.
    /// </summary>
    public string? When { get; set; }

    /// <summary>The instrument's unit, e.g. <c>ms</c>. Null passes no unit.</summary>
    public string? Unit { get; set; }

    /// <summary>The instrument's description. Null passes none.</summary>
    public string? Description { get; set; }

    /// <summary>Records call durations under <paramref name="metric"/>.</summary>
    /// <param name="metric">The metric name.</param>
    public HistogramAttribute(string metric) => Metric = metric;
}
```

Create `src/ZeroAlloc.Telemetry/CountFromResultAttribute.cs`:

```csharp
namespace ZeroAlloc.Telemetry;

/// <summary>
/// Adds a member of the return value to a <see cref="System.Diagnostics.Metrics.Counter{T}"/> of
/// <c>long</c> after a successful (non-throwing) call.
/// </summary>
/// <remarks>
/// <para>
/// For values only known once the call returns, such as tokens consumed, rows written or items
/// returned. The path is read from the awaited return value, exactly as
/// <see cref="TraceTagFromResultAttribute"/> reads it. On a <c>Result&lt;T, E&gt;</c>, the success
/// value is reached through <c>Value</c>, as in <c>Value.Usage.InputTokens</c>.
/// </para>
/// <para>
/// The member must convert implicitly to <c>long</c>: <c>sbyte</c>, <c>byte</c>, <c>short</c>,
/// <c>ushort</c>, <c>int</c>, <c>uint</c> or <c>long</c>, or a nullable form of one. Anything else
/// is reported as <c>ZTEL009</c>. A null value, or a null anywhere along the path, is not recorded.
/// </para>
/// <para>
/// A negative value is added as it is. Counters are expected to only increase, and a backend may
/// reject a decrease or report it as a reset, so count quantities that cannot go negative.
/// </para>
/// <para>
/// Does not require <see cref="TraceAttribute"/>: this is a metric, not span data. May be applied
/// more than once.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [CountFromResult("llm.tokens.input", "Value.Usage.InputTokens", When = "IsSuccess", Unit = "{token}")]
/// [CountFromResult("llm.tokens.output", "Value.Usage.OutputTokens", When = "IsSuccess", Unit = "{token}")]
/// ValueTask&lt;Result&lt;ChatResponse, ChatError&gt;&gt; CompleteAsync(ChatRequest request, CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CountFromResultAttribute : Attribute
{
    /// <summary>The metric name passed to <see cref="System.Diagnostics.Metrics.Meter.CreateCounter{T}(string, string?, string?)"/>.</summary>
    public string Metric { get; }

    /// <summary>
    /// Dotted path from the awaited return value to the member to add, e.g. <c>Value.Count</c>.
    /// Empty adds the return value itself.
    /// </summary>
    public string Member { get; }

    /// <summary>
    /// A dotted path to a <c>bool</c> or <c>bool?</c> member that must be true for the value to be
    /// added, e.g. <c>IsSuccess</c>. <see cref="Member"/> is not read otherwise. Null always adds.
    /// </summary>
    public string? When { get; set; }

    /// <summary>The instrument's unit, e.g. <c>{token}</c>. Null passes no unit.</summary>
    public string? Unit { get; set; }

    /// <summary>The instrument's description. Null passes none.</summary>
    public string? Description { get; set; }

    /// <summary>Adds <paramref name="member"/> of the return value to <paramref name="metric"/>.</summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="member">Dotted path from the awaited return value.</param>
    public CountFromResultAttribute(string metric, string member)
    {
        Metric = metric;
        Member = member;
    }
}
```

Create `src/ZeroAlloc.Telemetry/HistogramFromResultAttribute.cs`:

```csharp
namespace ZeroAlloc.Telemetry;

/// <summary>
/// Records a member of the return value in a
/// <see cref="System.Diagnostics.Metrics.Histogram{T}"/> of <c>double</c> after a successful
/// (non-throwing) call.
/// </summary>
/// <remarks>
/// <para>
/// For distributions only known once the call returns, such as a confidence score, a result size
/// or a cost. The path is read from the awaited return value, exactly as
/// <see cref="TraceTagFromResultAttribute"/> reads it.
/// </para>
/// <para>
/// The member must be numeric: <c>sbyte</c>, <c>byte</c>, <c>short</c>, <c>ushort</c>, <c>int</c>,
/// <c>uint</c>, <c>long</c>, <c>ulong</c>, <c>float</c>, <c>double</c> or <c>decimal</c>, or a
/// nullable form of one. Anything else is reported as <c>ZTEL009</c>. A <c>decimal</c> is
/// converted with an explicit cast to <c>double</c>. A null value, or a null anywhere along the
/// path, is not recorded.
/// </para>
/// <para>
/// A call that throws has no result, so nothing is recorded for it. Does not require
/// <see cref="TraceAttribute"/>. May be applied more than once.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [HistogramFromResult("answer.confidence", "Value.Confidence", When = "IsSuccess", Unit = "1")]
/// ValueTask&lt;Result&lt;Answer, AskError&gt;&gt; AskAsync(Question question, CancellationToken ct);
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class HistogramFromResultAttribute : Attribute
{
    /// <summary>The metric name passed to <see cref="System.Diagnostics.Metrics.Meter.CreateHistogram{T}(string, string?, string?)"/>.</summary>
    public string Metric { get; }

    /// <summary>
    /// Dotted path from the awaited return value to the member to record, e.g. <c>Value.Score</c>.
    /// Empty records the return value itself.
    /// </summary>
    public string Member { get; }

    /// <summary>
    /// A dotted path to a <c>bool</c> or <c>bool?</c> member that must be true for the value to be
    /// recorded, e.g. <c>IsSuccess</c>. <see cref="Member"/> is not read otherwise. Null always records.
    /// </summary>
    public string? When { get; set; }

    /// <summary>The instrument's unit, e.g. <c>1</c> for a ratio. Null passes no unit.</summary>
    public string? Unit { get; set; }

    /// <summary>The instrument's description. Null passes none.</summary>
    public string? Description { get; set; }

    /// <summary>Records <paramref name="member"/> of the return value in <paramref name="metric"/>.</summary>
    /// <param name="metric">The metric name.</param>
    /// <param name="member">Dotted path from the awaited return value.</param>
    public HistogramFromResultAttribute(string metric, string member)
    {
        Metric = metric;
        Member = member;
    }
}
```

- [ ] **Step 2: Build to verify RS0016 fails it**

Run: `dotnet build src/ZeroAlloc.Telemetry/ZeroAlloc.Telemetry.csproj -c Release`

Expected: FAIL with `error RS0016: Symbol 'CountFromResultAttribute' is not part of the declared public API`, and the same for every new member.

- [ ] **Step 3: Declare the API**

Replace `src/ZeroAlloc.Telemetry/PublicAPI.Unshipped.txt`:

```text
#nullable enable
ZeroAlloc.Telemetry.CountAttribute.Description.get -> string?
ZeroAlloc.Telemetry.CountAttribute.Description.set -> void
ZeroAlloc.Telemetry.CountAttribute.Unit.get -> string?
ZeroAlloc.Telemetry.CountAttribute.Unit.set -> void
ZeroAlloc.Telemetry.CountAttribute.When.get -> string?
ZeroAlloc.Telemetry.CountAttribute.When.set -> void
ZeroAlloc.Telemetry.CountFromResultAttribute
ZeroAlloc.Telemetry.CountFromResultAttribute.CountFromResultAttribute(string! metric, string! member) -> void
ZeroAlloc.Telemetry.CountFromResultAttribute.Description.get -> string?
ZeroAlloc.Telemetry.CountFromResultAttribute.Description.set -> void
ZeroAlloc.Telemetry.CountFromResultAttribute.Member.get -> string!
ZeroAlloc.Telemetry.CountFromResultAttribute.Metric.get -> string!
ZeroAlloc.Telemetry.CountFromResultAttribute.Unit.get -> string?
ZeroAlloc.Telemetry.CountFromResultAttribute.Unit.set -> void
ZeroAlloc.Telemetry.CountFromResultAttribute.When.get -> string?
ZeroAlloc.Telemetry.CountFromResultAttribute.When.set -> void
ZeroAlloc.Telemetry.HistogramAttribute.Description.get -> string?
ZeroAlloc.Telemetry.HistogramAttribute.Description.set -> void
ZeroAlloc.Telemetry.HistogramAttribute.Unit.get -> string?
ZeroAlloc.Telemetry.HistogramAttribute.Unit.set -> void
ZeroAlloc.Telemetry.HistogramAttribute.When.get -> string?
ZeroAlloc.Telemetry.HistogramAttribute.When.set -> void
ZeroAlloc.Telemetry.HistogramFromResultAttribute
ZeroAlloc.Telemetry.HistogramFromResultAttribute.Description.get -> string?
ZeroAlloc.Telemetry.HistogramFromResultAttribute.Description.set -> void
ZeroAlloc.Telemetry.HistogramFromResultAttribute.HistogramFromResultAttribute(string! metric, string! member) -> void
ZeroAlloc.Telemetry.HistogramFromResultAttribute.Member.get -> string!
ZeroAlloc.Telemetry.HistogramFromResultAttribute.Metric.get -> string!
ZeroAlloc.Telemetry.HistogramFromResultAttribute.Unit.get -> string?
ZeroAlloc.Telemetry.HistogramFromResultAttribute.Unit.set -> void
ZeroAlloc.Telemetry.HistogramFromResultAttribute.When.get -> string?
ZeroAlloc.Telemetry.HistogramFromResultAttribute.When.set -> void
```

Run: `dotnet build src/ZeroAlloc.Telemetry/ZeroAlloc.Telemetry.csproj -c Release`

Expected: `Build succeeded.` `0 Warning(s)` `0 Error(s)`.

- [ ] **Step 4: Write the failing generator tests**

In `MetricFieldTests.cs`, add:

```csharp
    private const string UnitSource = """
        using ZeroAlloc.Telemetry;
        using System.Threading;
        using System.Threading.Tasks;

        [Instrument("MyApp.Orders")]
        public interface IOrders
        {
            [Count("orders.created", Unit = "{order}", Description = "Orders accepted for fulfilment")]
            [Histogram("order.create_ms", Unit = "ms")]
            Task<int> CreateAsync(CancellationToken ct);

            // Shares the counter. Only the first non-null Unit and Description are used, so this
            // Description is ignored and the field above is unchanged.
            [Count("orders.created", Description = "ignored: a description was already declared")]
            [Histogram("order.create_ms", Description = "Time to create an order")]
            Task<int> CreateAgainAsync(CancellationToken ct);
        }
        """;

    [Fact]
    public void GeneratesUnitAndDescription_ForCountAndHistogram()
        => GeneratorSnapshot.Verify(RunGenerator(UnitSource));

    [Fact]
    public void PassesTheFirstUnitAndDescription_ToTheFactory()
    {
        var generated = RunGeneratorSource(UnitSource);

        Assert.Contains(
            "Counter<long> _orders_created = _meter.CreateCounter<long>(\"orders.created\", unit: \"{order}\", description: \"Orders accepted for fulfilment\");",
            generated, StringComparison.Ordinal);
        Assert.Contains(
            "Histogram<double> _order_create_ms = _meter.CreateHistogram<double>(\"order.create_ms\", unit: \"ms\", description: \"Time to create an order\");",
            generated, StringComparison.Ordinal);
        Assert.DoesNotContain("ignored:", generated, StringComparison.Ordinal);
    }
```

In `DiagnosticTests.cs`, add:

```csharp
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
```

- [ ] **Step 5: Run to verify failure**

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~MetricFieldTests|FullyQualifiedName~ZTEL003"`

Expected: FAIL.
- `PassesTheFirstUnitAndDescription_ToTheFactory` fails on the first `Assert.Contains`.
- The ZTEL003 theory fails with `The collection was empty`.
- The unit snapshot is missing.

- [ ] **Step 6: Read `Unit`/`Description` and register ZTEL003**

In `InstrumentGenerator.cs`, add these constants after `TraceTagConstantAttrFqn`:

```csharp
    private const string CountFromResultAttrFqn     = "ZeroAlloc.Telemetry.CountFromResultAttribute";
    private const string HistogramFromResultAttrFqn = "ZeroAlloc.Telemetry.HistogramFromResultAttribute";
```

In `Initialize`, update the ZTEL003 comment to say "method-level metric and trace attributes". Then, after the `HistogramAttributeFqn` registration, add:

```csharp
        RegisterMethodAttributeDiagnostic(context, CountFromResultAttrFqn, "CountFromResult");
        RegisterMethodAttributeDiagnostic(context, HistogramFromResultAttrFqn, "HistogramFromResult");
```

In `BuildMethods`, delete `var countMetric = ...` and `var histMetric = ...`. Replace the two `MetricModel` arguments of `new MethodModel(...)` with `count,` and `histogram,`, and declare them before the call:

```csharp
            var count     = BuildPlainMetric(member, CountAttributeFqn, MetricKind.Counter);
            var histogram = BuildPlainMetric(member, HistogramAttributeFqn, MetricKind.Histogram);
```

Add:

```csharp
    /// <summary>Builds the model for <c>[Count]</c> or <c>[Histogram]</c>, or null when the method has none.</summary>
    private static MetricModel? BuildPlainMetric(IMethodSymbol method, string attributeFqn, MetricKind kind)
    {
        var attr = FindAttribute(method, attributeFqn);
        if (attr is null || attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Value is not string metric)
            return null;

        return new MetricModel(kind, metric, GetNamedString(attr, "Unit"), GetNamedString(attr, "Description"));
    }

    private static AttributeData? FindAttribute(IMethodSymbol method, string attributeFqn)
    {
        foreach (var attr in method.GetAttributes())
        {
            if (IsAttribute(attr, attributeFqn))
                return attr;
        }

        return null;
    }
```

`GetAttributeFirstArg` is still used for `[Trace]`, so keep it.

In `InstrumentDiagnostics.cs`, change the ZTEL003 `title` to `"Telemetry attribute on a method in a type without [Instrument] is ignored"`.

Replace `Models/MetricModel.cs`:

```csharp
namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>One use of an instrument by one method.</summary>
/// <param name="Kind">Which instrument, and so which field type.</param>
/// <param name="Metric">The metric name passed to the <c>Meter</c> factory.</param>
/// <param name="Unit">Passed to the factory as <c>unit:</c>; null passes none.</param>
/// <param name="Description">Passed to the factory as <c>description:</c>; null passes none.</param>
internal sealed record MetricModel(
    MetricKind Kind,
    string Metric,
    string? Unit = null,
    string? Description = null);
```

In `MetricFieldTable.cs`, replace the `Field` record and the inner loop of `Build`:

```csharp
    /// <summary>A field the proxy declares.</summary>
    /// <param name="Kind">Field type.</param>
    /// <param name="Metric">Metric name passed to the <c>Meter</c> factory.</param>
    /// <param name="FieldName">The full identifier, including its leading underscore.</param>
    /// <param name="Unit">The first non-null unit declared for this instrument, in declaration order.</param>
    /// <param name="Description">The first non-null description declared for this instrument.</param>
    internal sealed record Field(MetricKind Kind, string Metric, string FieldName, string? Unit, string? Description);
```
```csharp
            foreach (var metric in MetricsOf(method))
            {
                var key = new MetricKey(metric.Kind, metric.Metric);
                if (indexByKey.TryGetValue(key, out var index))
                {
                    // One instrument, several declarations: the first non-null of each wins, so
                    // either can be declared once on any of them.
                    var existing = fields[index];
                    fields[index] = existing with
                    {
                        Unit = existing.Unit ?? metric.Unit,
                        Description = existing.Description ?? metric.Description,
                    };
                    continue;
                }

                indexByKey.Add(key, fields.Count);
                fields.Add(new Field(metric.Kind, metric.Metric, AssignName(metric.Metric, used), metric.Unit, metric.Description));
            }
```

In `ProxyWriter.WriteMetricFields`, replace the `AppendLine`:

```csharp
            sb.AppendLine($"    private static readonly {type} {field.FieldName} = _meter.{factory}({FactoryArguments(field)});");
```

Add:

```csharp
    /// <summary>
    /// Arguments to <c>CreateCounter</c>/<c>CreateHistogram</c>. Unit and description are named
    /// and only passed when set, so an instrument without them emits the same call as before.
    /// </summary>
    private static string FactoryArguments(MetricFieldTable.Field field)
    {
        var args = Literal(field.Metric);
        if (field.Unit is not null)
            args += ", unit: " + Literal(field.Unit);
        if (field.Description is not null)
            args += ", description: " + Literal(field.Description);

        return args;
    }
```

- [ ] **Step 7: Accept the snapshot and run everything**

Run: `ZA_SNAPSHOT_UPDATE=1 dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~GeneratesUnitAndDescription"`

The field block of `MetricFieldTests.GeneratesUnitAndDescription_ForCountAndHistogram#OrdersInstrumented.g.verified.cs` must read:

```csharp
    private static readonly Counter<long> _orders_created = _meter.CreateCounter<long>("orders.created", unit: "{order}", description: "Orders accepted for fulfilment");
    private static readonly Histogram<double> _order_create_ms = _meter.CreateHistogram<double>("order.create_ms", unit: "ms", description: "Time to create an order");
```

Run: `dotnet test ZeroAlloc.Telemetry.slnx -c Release`

Expected: every test project reports `Failed: 0`. The build shows `0 Warning(s)`, which also covers the benchmarks and the AOT sample.

- [ ] **Step 8: Commit**

```bash
git add src tests
git commit -F - <<'EOF'
feat(core): add CountFromResult, HistogramFromResult, and When, Unit and Description

CountFromResult and HistogramFromResult record a member of the return value on a counter or a
histogram. Count and Histogram gain When, to record only when a boolean member of the result is
true, and all four attributes gain Unit and Description.

This change adds the public API and ZTEL003 for the new attributes on a type without
[Instrument], and passes Unit and Description through to the Meter factory. When and the
result-driven instruments are emitted in the next change.

Refs #142

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 5: Emit result-driven instruments and guards, ZTEL009, and ZTEL005 for the new cases

**Files:**
- Modify: `src/ZeroAlloc.Telemetry.Generator/Models/MetricModel.cs`
- Modify: `src/ZeroAlloc.Telemetry.Generator/Models/MethodModel.cs`, adding `ResultMetrics`
- Modify: `src/ZeroAlloc.Telemetry.Generator/MetricFieldTable.cs`: `MetricsOf`, and the `_read` reservation
- Modify: `src/ZeroAlloc.Telemetry.Generator/InstrumentGenerator.cs`: `BuildMethods`, `BuildPlainMetric`, and the new `BuildResultMetrics` and `Fits`
- Modify: `src/ZeroAlloc.Telemetry.Generator/ProxyWriter.cs`: `WriteMethod`, `WriteInstrumentedBody`, `WriteResultTags` (renamed to `WriteResultReads`) and `WriteCatchBlock`
- Modify: `src/ZeroAlloc.Telemetry.Generator/InstrumentDiagnostics.cs` and `AnalyzerReleases.Unshipped.md`
- Create: `tests/ZeroAlloc.Telemetry.Generator.Tests/ResultMetricTests.cs`
- Modify: `tests/ZeroAlloc.Telemetry.Generator.Tests/DiagnosticTests.cs` and `GeneratedCodeCompilesTests.cs`

**Interfaces:**
- Consumes:
  - `PathResolver`, `AttributeLocations`, `TryBuildGuard`, `PathNotFound`, `IsAttribute`, `GetNamedString` and `MethodLocation` from Task 3.
  - `FindAttribute`, `CountFromResultAttrFqn` and `HistogramFromResultAttrFqn` from Task 4.
  - `MetricFieldTable.FieldFor` from Task 1.
- Produces:
  - `MetricModel(MetricKind Kind, string Metric, string? Unit = null, string? Description = null, string? GuardExpression = null, string? ValueAccess = null, bool ValueCanBeNull = false, bool ValueIsDecimal = false)`.
  - `MethodModel`: `IReadOnlyList<MetricModel> ResultMetrics`, inserted after `ConstantTags`.
  - `InstrumentDiagnostics.MemberDoesNotFitInstrument` (ZTEL009).

**Emitted shape.** This is the contract the snapshots below pin.
- After `var _result = ...;`, and inside the `try`, the writer emits `var _tagged = _result;` once, when the result can be null and anything reads it.
- It then emits, in order: result tags if there is a span, result-driven instruments in attribute order, then `[Count]`, then `[Histogram]`.
- A value that can be null is read with `is { } _readN`, so a null is skipped and the local is non-nullable.
- A guard is joined with `&&`, so the member is not read when the guard is false.
- A `decimal` is cast with `(double)`.
- A guarded `[Histogram]` emits no `Record` in the `catch`.

- [ ] **Step 1: Write the failing snapshot tests**

Create `tests/ZeroAlloc.Telemetry.Generator.Tests/ResultMetricTests.cs`:

```csharp
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Covers <c>[CountFromResult]</c>, <c>[HistogramFromResult]</c> and <c>When</c> on
/// <c>[Count]</c>/<c>[Histogram]</c> (issue #142).
/// </summary>
public class ResultMetricTests
{
    /// <summary>
    /// A Result that is a class: nullable at every hop, so every read is null-safe, and a span tag
    /// and three instruments share one <c>_tagged</c> copy. Also covers two counters on one
    /// method, a <c>long?</c>, a <c>decimal</c>, and <c>When</c>, <c>Unit</c> and <c>Description</c>.
    /// </summary>
    [Fact]
    public void GeneratesResultMetrics_ForResultReturn()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            public sealed class LlmError { }

            public sealed class Result<T, E>
            {
                public bool IsSuccess { get; }
                public T Value { get; } = default!;
            }

            public sealed class TokenUsage
            {
                public int Input { get; set; }
                public long? Output { get; set; }
                public decimal Cost { get; set; }
            }

            [Instrument("MyApp.Llm")]
            public interface IChat
            {
                [Trace("llm.complete")]
                [TraceTagFromResult("gen_ai.usage.input_tokens", "Value.Input", When = "IsSuccess")]
                [CountFromResult("llm.tokens.input", "Value.Input", When = "IsSuccess", Unit = "{token}", Description = "Prompt tokens consumed")]
                [CountFromResult("llm.tokens.output", "Value.Output", When = "IsSuccess", Unit = "{token}")]
                [HistogramFromResult("llm.cost", "Value.Cost", When = "IsSuccess", Unit = "USD")]
                Task<Result<TokenUsage, LlmError>> CompleteAsync(string prompt, CancellationToken ct);
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    /// <summary>
    /// <c>When</c> on <c>[Count]</c> and <c>[Histogram]</c>. A guarded histogram records on the
    /// success path only: a throw leaves no result to evaluate the guard against.
    /// </summary>
    [Fact]
    public void GeneratesGuardedCountAndHistogram()
    {
        var generated = RunGeneratorSource(GuardedSource);

        // One Record, in the try. The catch sets the span status and rethrows, nothing more.
        Assert.Equal(1, CountOccurrences(generated, "_orders_accept_ms.Record("));
        GeneratorSnapshot.Verify(RunGenerator(GuardedSource));
    }

    /// <summary>
    /// Plain returns rather than a Result: a struct root with a bare guard and a decimal cast, a
    /// class root without a guard, an empty member reading the value itself, and a nullable int
    /// read through Value.
    /// </summary>
    [Fact]
    public void GeneratesResultMetrics_ForPlainAndValueTypeReturns()
    {
        var source = """
            using ZeroAlloc.Telemetry;
            using System.Threading;
            using System.Threading.Tasks;

            public readonly struct Quote
            {
                public bool Priced { get; }
                public decimal Cost { get; }
            }

            public sealed class SearchPage { public int Hits { get; set; } }

            [Instrument("MyApp.Plain")]
            public interface IPlain
            {
                [HistogramFromResult("quote.cost", "Cost", When = "Priced")]
                ValueTask<Quote> QuoteAsync(CancellationToken ct);

                [HistogramFromResult("search.hits", "Hits")]
                Task<SearchPage> SearchAsync(CancellationToken ct);

                [CountFromResult("batch.items", "")]
                Task<int> BatchAsync(CancellationToken ct);

                [CountFromResult("maybe.items", "Value")]
                Task<int?> MaybeAsync(CancellationToken ct);
            }
            """;

        GeneratorSnapshot.Verify(RunGenerator(source));
    }

    private const string GuardedSource = """
        using ZeroAlloc.Telemetry;
        using System.Threading;
        using System.Threading.Tasks;

        public sealed class OrderError { }

        public sealed class Result<T, E>
        {
            public bool IsSuccess { get; }
            public T Value { get; } = default!;
        }

        [Instrument("MyApp.Orders")]
        public interface IOrders
        {
            [Trace("orders.accept")]
            [Count("orders.accepted", When = "IsSuccess")]
            [Histogram("orders.accept_ms", When = "IsSuccess", Unit = "ms")]
            Task<Result<string, OrderError>> AcceptAsync(string orderId, CancellationToken ct);
        }
        """;

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }

    private static string RunGeneratorSource(string source)
    {
        // Plain loop rather than LINQ: EPS06 flags Select over ImmutableArray as a hidden copy.
        var sb = new System.Text.StringBuilder();
        foreach (var tree in RunGenerator(source).GetRunResult().GeneratedTrees)
            sb.AppendLine(tree.ToString());

        return sb.ToString();
    }

    private static GeneratorDriver RunGenerator(string source)
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        var runtimeRefs = trustedPlatformAssemblies
            .Split(System.IO.Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => MetadataReference.CreateFromFile(p))
            .ToArray();

        var compilation = CSharpCompilation.Create("TestAssembly",
            [CSharpSyntaxTree.ParseText(source)],
            runtimeRefs.Concat<MetadataReference>(
            [
                MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location),
            ]),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        return CSharpGeneratorDriver.Create(new InstrumentGenerator()).RunGenerators(compilation);
    }
}
```

- [ ] **Step 2: Write the failing diagnostic tests**

In `DiagnosticTests.cs`, add these `InlineData` rows to `ZTEL007_UnresolvedSegment_IsReportedAtTheArgument`:

```csharp
    [InlineData("[CountFromResult(\"m\", \"Totl\")]")]
    [InlineData("[HistogramFromResult(\"m\", \"Totl\")]")]
    [InlineData("[CountFromResult(\"m\", \"Total\", When = \"Totl\")]")]
    [InlineData("[HistogramFromResult(\"m\", \"Total\", When = \"Totl\")]")]
    [InlineData("[Count(\"m\", When = \"Totl\")]")]
    [InlineData("[Histogram(\"m\", When = \"Totl\")]")]
```

Add to `ZTEL008_NonBooleanWhen_ProducesError`:

```csharp
    [InlineData("[CountFromResult(\"m\", \"Total\", When = \"Count\")]")]
    [InlineData("[HistogramFromResult(\"m\", \"Total\", When = \"Count\")]")]
    [InlineData("[Count(\"m\", When = \"Count\")]")]
    [InlineData("[Histogram(\"m\", When = \"Count\")]")]
```

Add:

```csharp
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
        var diagnostics = RunAndCollectDiagnostics($$"""
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
            """);

        var d = Single(diagnostics, "ZTEL009");
        Assert.Equal(DiagnosticSeverity.Error, d.Severity);
        Assert.Equal($"\"{member}\"", LocationText(d));
        Assert.Equal(
            $"[{attribute}] cannot record '{member}' of type '{typeName}'. {reason}.",
            d.GetMessage(CultureInfo.InvariantCulture));
    }

    [Fact]
    public void ZTEL005_ResultReadsOnMethodWithoutResult_NameTheAttribute()
    {
        var diagnostics = RunAndCollectDiagnostics("""
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
            """);

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
    }
```

- [ ] **Step 3: Write the failing compile probe**

In `GeneratedCodeCompilesTests.cs`, add:

```csharp
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
```

- [ ] **Step 4: Run to verify failure**

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~ResultMetricTests|FullyQualifiedName~DiagnosticTests|FullyQualifiedName~ResultMetricsProbe"`

Expected: FAIL.
- The snapshots are missing.
- `GeneratesGuardedCountAndHistogram` fails `Assert.Equal(1, ...)` with `Actual: 2`, because 1.6.4 records in the catch.
- The ZTEL009 and ZTEL005 tests fail, and the new ZTEL007/ZTEL008 rows report an empty collection.
- The probe compiles, because nothing is emitted yet, so it passes vacuously. Step 9 is where it proves the emitted code compiles.

- [ ] **Step 5: Extend the model**

Replace `Models/MetricModel.cs`:

```csharp
namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>One use of an instrument by one method.</summary>
/// <param name="Kind">Which instrument, and so which field type.</param>
/// <param name="Metric">The metric name passed to the <c>Meter</c> factory.</param>
/// <param name="Unit">Passed to the factory as <c>unit:</c>; null passes none.</param>
/// <param name="Description">Passed to the factory as <c>description:</c>; null passes none.</param>
/// <param name="GuardExpression">
/// The resolved <c>When</c> condition to append to the result root, such as
/// <c>?.IsSuccess == true</c>. Null records unconditionally.
/// </param>
/// <param name="ValueAccess">
/// For <c>[CountFromResult]</c>/<c>[HistogramFromResult]</c>, the resolved member access to append
/// to the result root; empty reads the root itself. Null for <c>[Count]</c>, which adds 1, and for
/// <c>[Histogram]</c>, which records elapsed milliseconds.
/// </param>
/// <param name="ValueCanBeNull">
/// Whether the value expression can be null. It is then read with <c>is { } _readN</c>, which
/// skips a null and gives a non-nullable local.
/// </param>
/// <param name="ValueIsDecimal">
/// <c>decimal</c> or <c>decimal?</c>, which has no implicit conversion to <c>double</c> and is cast.
/// </param>
internal sealed record MetricModel(
    MetricKind Kind,
    string Metric,
    string? Unit = null,
    string? Description = null,
    string? GuardExpression = null,
    string? ValueAccess = null,
    bool ValueCanBeNull = false,
    bool ValueIsDecimal = false);
```

In `Models/MethodModel.cs`, insert after `IReadOnlyList<ConstantTagModel> ConstantTags,`:

```csharp
    IReadOnlyList<MetricModel> ResultMetrics,
```

Add its `<param>`:

```csharp
/// <param name="ResultMetrics">
/// <c>[CountFromResult]</c> and <c>[HistogramFromResult]</c> uses, in attribute order. Only the
/// ones that resolved and type-checked; the rest were reported and are not emitted.
/// </param>
```

In `MetricFieldTable.cs`, add a third `yield` to `MetricsOf`:

```csharp
        foreach (var metric in method.ResultMetrics)
            yield return metric;
```

In `IsReserved`, add the value locals the writer now emits:

```csharp
    private static bool IsReserved(string name) =>
        ReservedNames.Contains(name)
        || name.StartsWith("_spanName_", StringComparison.Ordinal)
        || name.StartsWith("_tag_", StringComparison.Ordinal)
        || IsReadLocal(name);

    /// <summary><c>_read0</c>, <c>_read1</c> …: the pattern locals holding a non-null metric value.</summary>
    private static bool IsReadLocal(string name)
    {
        const string prefix = "_read";
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length == prefix.Length)
            return false;

        for (var i = prefix.Length; i < name.Length; i++)
        {
            if (name[i] < '0' || name[i] > '9')
                return false;
        }

        return true;
    }
```

- [ ] **Step 6: Add ZTEL009**

In `InstrumentDiagnostics.cs`, add:

```csharp
    public static readonly DiagnosticDescriptor MemberDoesNotFitInstrument = new(
        id: "ZTEL009",
        title: "Member type does not fit the instrument",
        messageFormat: "[{0}] cannot record {1} of type '{2}'. {3}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);
```

In `AnalyzerReleases.Unshipped.md`, append the row:

```text
ZTEL009 | ZeroAlloc.Telemetry | Error    | A result-driven metric's member does not fit its instrument
```

- [ ] **Step 7: Build the result-driven models**

In `InstrumentGenerator.cs`, change `BuildMethods` so the three metric builders get the result type and the diagnostics:

```csharp
            var count         = BuildPlainMetric(target, member, CountAttributeFqn, "Count", MetricKind.Counter, resultType, returnsVoid, diagnostics);
            var histogram     = BuildPlainMetric(target, member, HistogramAttributeFqn, "Histogram", MetricKind.Histogram, resultType, returnsVoid, diagnostics);
            var resultMetrics = BuildResultMetrics(target, member, resultType, returnsVoid, diagnostics);
```

Pass `resultMetrics` to `new MethodModel(...)` directly after `constantTags`.

Replace `BuildPlainMetric`:

```csharp
    /// <summary>
    /// Builds the model for <c>[Count]</c> or <c>[Histogram]</c>. Null when the method has none, or
    /// when its <c>When</c> guard cannot be emitted.
    /// </summary>
    private static MetricModel? BuildPlainMetric(
        INamedTypeSymbol target,
        IMethodSymbol method,
        string attributeFqn,
        string shortName,
        MetricKind kind,
        ITypeSymbol resultType,
        bool returnsVoid,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        var attr = FindAttribute(method, attributeFqn);
        if (attr is null || attr.ConstructorArguments.Length == 0 || attr.ConstructorArguments[0].Value is not string metric)
            return null;

        var when = GetNamedString(attr, "When");
        string? guard = null;
        if (!string.IsNullOrWhiteSpace(when))
        {
            // A guard needs a result to read. Recording unguarded instead would count exactly the
            // calls the guard was written to exclude, so the instrument records nothing.
            if (returnsVoid)
            {
                diagnostics.Add(Diagnostic.Create(
                    InstrumentDiagnostics.ResultReadOnVoidMethod,
                    MethodLocation(method),
                    $"{shortName}(When = {Microsoft.CodeAnalysis.CSharp.SymbolDisplay.FormatLiteral(when!, quote: true)})",
                    target.ToDisplayString(),
                    method.Name));
                return null;
            }

            if (!TryBuildGuard(attr, method, resultType, when, diagnostics, out guard))
                return null;
        }

        return new MetricModel(kind, metric, GetNamedString(attr, "Unit"), GetNamedString(attr, "Description"), guard);
    }

    private const string CounterTypesReason =
        "A counter adds long values, so the member must be sbyte, byte, short, ushort, int, uint or long, or a nullable form of one";

    private const string HistogramTypesReason =
        "A histogram records double values, so the member must be sbyte, byte, short, ushort, int, uint, long, ulong, float, double or decimal, or a nullable form of one";

    /// <summary>
    /// Builds <c>[CountFromResult]</c> and <c>[HistogramFromResult]</c> in attribute order. One that
    /// cannot be emitted is reported and left out, so its diagnostic is the only error.
    /// </summary>
    private static MetricModel[] BuildResultMetrics(
        INamedTypeSymbol target,
        IMethodSymbol method,
        ITypeSymbol resultType,
        bool returnsVoid,
        ImmutableArray<Diagnostic>.Builder diagnostics)
    {
        List<MetricModel>? metrics = null;
        foreach (var attr in method.GetAttributes())
        {
            MetricKind kind;
            string shortName;
            if (IsAttribute(attr, CountFromResultAttrFqn))
            {
                kind = MetricKind.Counter;
                shortName = "CountFromResult";
            }
            else if (IsAttribute(attr, HistogramFromResultAttrFqn))
            {
                kind = MetricKind.Histogram;
                shortName = "HistogramFromResult";
            }
            else
            {
                continue;
            }

            if (attr.ConstructorArguments.Length < 2 || attr.ConstructorArguments[0].Value is not string metric)
                continue;

            if (returnsVoid)
            {
                diagnostics.Add(Diagnostic.Create(
                    InstrumentDiagnostics.ResultReadOnVoidMethod,
                    MethodLocation(method), shortName, target.ToDisplayString(), method.Name));
                continue;
            }

            var member = attr.ConstructorArguments[1].Value as string ?? string.Empty;
            var memberLocation = AttributeLocations.Positional(attr, 1, "member", MethodLocation(method));

            var path = PathResolver.Resolve(resultType, member);
            if (!path.Resolved)
            {
                diagnostics.Add(PathNotFound(memberLocation, member, path));
                continue;
            }

            var valueType = path.FinalType!;
            if (!Fits(kind, valueType))
            {
                diagnostics.Add(Diagnostic.Create(
                    InstrumentDiagnostics.MemberDoesNotFitInstrument,
                    memberLocation,
                    shortName,
                    member.Length == 0 ? "the return value" : $"'{member}'",
                    valueType.ToDisplayString(),
                    kind == MetricKind.Counter ? CounterTypesReason : HistogramTypesReason));
                continue;
            }

            if (!TryBuildGuard(attr, method, resultType, GetNamedString(attr, "When"), diagnostics, out var guard))
                continue;

            (metrics ??= new List<MetricModel>()).Add(new MetricModel(
                kind,
                metric,
                GetNamedString(attr, "Unit"),
                GetNamedString(attr, "Description"),
                guard,
                path.Access,
                path.CanBeNull,
                PathResolver.UnwrapNullable(valueType).SpecialType == SpecialType.System_Decimal));
        }

        return metrics?.ToArray() ?? [];
    }

    /// <summary>
    /// Whether the value can go to the instrument without a cast the user did not write, or with
    /// the explicit decimal cast the spec calls for.
    /// </summary>
    /// <remarks>
    /// Explicit SpecialType sets rather than <c>Compilation.ClassifyConversion</c>. That would also
    /// admit <c>char</c>, and any type with a user-defined implicit conversion, and neither is a
    /// quantity a counter or a histogram should silently accept.
    /// </remarks>
    private static bool Fits(MetricKind kind, ITypeSymbol type)
    {
        var special = PathResolver.UnwrapNullable(type).SpecialType;

        var convertsToLong = special is SpecialType.System_SByte or SpecialType.System_Byte
            or SpecialType.System_Int16 or SpecialType.System_UInt16
            or SpecialType.System_Int32 or SpecialType.System_UInt32
            or SpecialType.System_Int64;

        if (kind == MetricKind.Counter)
            return convertsToLong;

        return convertsToLong
            || special is SpecialType.System_UInt64 or SpecialType.System_Single
                or SpecialType.System_Double or SpecialType.System_Decimal;
    }
```

- [ ] **Step 8: Emit them**

In `ProxyWriter.cs`, `WriteMethod`, extend `needsTry`:

```csharp
        var needsTry = method.TraceName is not null
                    || method.Histogram is not null
                    || method.Count is not null
                    || method.ResultMetrics.Count > 0;
```

In `WriteInstrumentedBody`, replace everything from the `// Result tags come first` comment through the `[Histogram]` record:

```csharp
        // Tags and result-driven instruments read the result, so they come first and share one
        // null-state copy. [Count] and [Histogram] follow, guarded when they have a When.
        if (!method.ReturnsVoid)
            WriteResultReads(sb, method, fields);

        if (method.Count is { } count)
            WriteGuarded(sb, method, count.GuardExpression, $"{fields.FieldFor(count)}.Add(1);");

        if (method.Histogram is { } histogram)
        {
            WriteGuarded(
                sb, method, histogram.GuardExpression,
                $"{fields.FieldFor(histogram)}.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);");
        }
```

Rename `WriteResultTags` to `WriteResultReads`. Replace its body, keeping the existing `<remarks>` about the copy:

```csharp
    private static void WriteResultReads(StringBuilder sb, MethodModel method, MetricFieldTable fields)
    {
        // Tags need a span to carry them; ZTEL004 reports a result tag without one.
        IReadOnlyList<ResultTagModel> tags = method.TraceName is not null
            ? method.ResultTags
            : Array.Empty<ResultTagModel>();

        // Every read that null-tests the result goes through the copy: a member access, a guard,
        // or a metric value, which is always read through the root.
        var needsCopy = method.ResultCanBeNull
            && (tags.Any(t => !string.IsNullOrEmpty(t.Member) || t.GuardExpression is not null)
                || method.ResultMetrics.Count > 0
                || method.Count?.GuardExpression is not null
                || method.Histogram?.GuardExpression is not null);

        if (needsCopy)
            sb.AppendLine("            var _tagged = _result;");

        var root = method.ResultCanBeNull ? "_tagged" : "_result";

        foreach (var tag in tags)
        {
            // No member records the result itself: no member access, so no null test and no
            // effect on _result's null-state. Otherwise the generator resolved the path, choosing
            // the operator for every segment, and reported ZTEL007 instead where it could not.
            var access = string.IsNullOrEmpty(tag.Member) ? "_result" : root + tag.AccessSuffix;

            if (tag.GuardExpression is { } guard)
            {
                // The guard has to prevent the member being read at all, which a null-conditional
                // cannot: `?.` protects against a null result, not against a result whose value is
                // unset. Reading Value on a failed Result is meaningless at best and throws at worst.
                sb.AppendLine($"            if ({root}{guard})");
                sb.AppendLine($"                _activity?.SetTag({Literal(tag.TagName)}, {access});");
            }
            else
            {
                sb.AppendLine($"            _activity?.SetTag({Literal(tag.TagName)}, {access});");
            }
        }

        for (var i = 0; i < method.ResultMetrics.Count; i++)
        {
            var metric = method.ResultMetrics[i];
            var access = root + metric.ValueAccess;
            var guard = metric.GuardExpression is { } g ? root + g : null;

            // A value that can be null is bound by a pattern: a null is skipped, and the local is
            // non-nullable, so it converts to long or double without a cast. The guard comes first
            // and short-circuits, so the member is not read at all when the guard is false.
            string? condition;
            string value;
            if (metric.ValueCanBeNull)
            {
                var local = "_read" + i.ToString(CultureInfo.InvariantCulture);
                var test = $"{access} is {{ }} {local}";
                condition = guard is null ? test : $"{guard} && {test}";
                value = local;
            }
            else
            {
                condition = guard;
                value = access;
            }

            if (metric.ValueIsDecimal)
                value = "(double)" + value;

            var call = metric.Kind == MetricKind.Counter
                ? $"{fields.FieldFor(metric)}.Add({value});"
                : $"{fields.FieldFor(metric)}.Record({value});";

            if (condition is null)
            {
                sb.AppendLine($"            {call}");
            }
            else
            {
                sb.AppendLine($"            if ({condition})");
                sb.AppendLine($"                {call}");
            }
        }
    }

    /// <summary>Emits <paramref name="statement"/>, under the guard when there is one.</summary>
    private static void WriteGuarded(StringBuilder sb, MethodModel method, string? guard, string statement)
    {
        if (guard is null)
        {
            sb.AppendLine($"            {statement}");
            return;
        }

        var root = method.ResultCanBeNull ? "_tagged" : "_result";
        sb.AppendLine($"            if ({root}{guard})");
        sb.AppendLine($"                {statement}");
    }
```

Add `using System.Globalization;` at the top of `ProxyWriter.cs`.

In `WriteCatchBlock`, replace the histogram block:

```csharp
        // A guarded histogram records only when its guard holds, and a throw leaves no result to
        // evaluate it against. An unguarded one records on both paths, as it always has.
        if (method.Histogram is { GuardExpression: null } histogram)
            sb.AppendLine($"            {fields.FieldFor(histogram)}.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);");
```

- [ ] **Step 9: Accept the snapshots and review them**

Run: `ZA_SNAPSHOT_UPDATE=1 dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj --filter "FullyQualifiedName~ResultMetricTests"`

`ResultMetricTests.GeneratesResultMetrics_ForResultReturn#ChatInstrumented.g.verified.cs` must read exactly as follows. It shows `[CountFromResult]`, `[HistogramFromResult]` with a `decimal`, two instruments on one method, and `_tagged` reused by a tag and three instruments.

```csharp
//HintName: ChatInstrumented.g.cs
// <auto-generated />
#pragma warning disable EPC12 // catch reads _ex.Message for span status, then rethrows
#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

internal sealed class ChatInstrumented : IChat
{
    private static readonly ActivitySource _activitySource = new("MyApp.Llm");
    private static readonly Meter _meter = new("MyApp.Llm");
    private static readonly Counter<long> _llm_tokens_input = _meter.CreateCounter<long>("llm.tokens.input", unit: "{token}", description: "Prompt tokens consumed");
    private static readonly Counter<long> _llm_tokens_output = _meter.CreateCounter<long>("llm.tokens.output", unit: "{token}");
    private static readonly Histogram<double> _llm_cost = _meter.CreateHistogram<double>("llm.cost", unit: "USD");

    private readonly IChat _inner;
    public ChatInstrumented(IChat inner) => _inner = inner;

    public async global::System.Threading.Tasks.Task<global::Result<global::TokenUsage, global::LlmError>> CompleteAsync(string prompt, global::System.Threading.CancellationToken ct)
    {
        using var _activity = _activitySource.StartActivity("llm.complete");
        try
        {
            var _result = await _inner.CompleteAsync(prompt, ct);
            var _tagged = _result;
            if (_tagged?.IsSuccess == true)
                _activity?.SetTag("gen_ai.usage.input_tokens", _tagged?.Value?.Input);
            if (_tagged?.IsSuccess == true && _tagged?.Value?.Input is { } _read0)
                _llm_tokens_input.Add(_read0);
            if (_tagged?.IsSuccess == true && _tagged?.Value?.Output is { } _read1)
                _llm_tokens_output.Add(_read1);
            if (_tagged?.IsSuccess == true && _tagged?.Value?.Cost is { } _read2)
                _llm_cost.Record((double)_read2);
            return _result;
        }
        catch (Exception _ex)
        {
            _activity?.SetStatus(ActivityStatusCode.Error, _ex.Message);
            throw;
        }
    }
}
```

`ResultMetricTests.GeneratesGuardedCountAndHistogram#OrdersInstrumented.g.verified.cs` must read as follows. It shows a guarded `[Count]`, and a guarded `[Histogram]` that records on the success path with no `Record` in the catch.

```csharp
//HintName: OrdersInstrumented.g.cs
// <auto-generated />
#pragma warning disable EPC12 // catch reads _ex.Message for span status, then rethrows
#nullable enable

using System;
using System.Diagnostics;
using System.Diagnostics.Metrics;

internal sealed class OrdersInstrumented : IOrders
{
    private static readonly ActivitySource _activitySource = new("MyApp.Orders");
    private static readonly Meter _meter = new("MyApp.Orders");
    private static readonly Counter<long> _orders_accepted = _meter.CreateCounter<long>("orders.accepted");
    private static readonly Histogram<double> _orders_accept_ms = _meter.CreateHistogram<double>("orders.accept_ms", unit: "ms");

    private readonly IOrders _inner;
    public OrdersInstrumented(IOrders inner) => _inner = inner;

    public async global::System.Threading.Tasks.Task<global::Result<string, global::OrderError>> AcceptAsync(string orderId, global::System.Threading.CancellationToken ct)
    {
        using var _activity = _activitySource.StartActivity("orders.accept");
        var _sw = Stopwatch.GetTimestamp();
        try
        {
            var _result = await _inner.AcceptAsync(orderId, ct);
            var _tagged = _result;
            if (_tagged?.IsSuccess == true)
                _orders_accepted.Add(1);
            if (_tagged?.IsSuccess == true)
                _orders_accept_ms.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);
            return _result;
        }
        catch (Exception _ex)
        {
            _activity?.SetStatus(ActivityStatusCode.Error, _ex.Message);
            throw;
        }
    }
}
```

The method bodies in `ResultMetricTests.GeneratesResultMetrics_ForPlainAndValueTypeReturns#PlainInstrumented.g.verified.cs` must read as follows, with the `catch (Exception) { throw; }` blocks after each. The fields are `_quote_cost` (Histogram), `_search_hits` (Histogram), `_batch_items` (Counter) and `_maybe_items` (Counter), none with unit or description.

```csharp
    public async global::System.Threading.Tasks.ValueTask<global::Quote> QuoteAsync(global::System.Threading.CancellationToken ct)
    {
        try
        {
            var _result = await _inner.QuoteAsync(ct);
            if (_result.Priced)
                _quote_cost.Record((double)_result.Cost);
            return _result;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async global::System.Threading.Tasks.Task<global::SearchPage> SearchAsync(global::System.Threading.CancellationToken ct)
    {
        try
        {
            var _result = await _inner.SearchAsync(ct);
            var _tagged = _result;
            if (_tagged?.Hits is { } _read0)
                _search_hits.Record(_read0);
            return _result;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async global::System.Threading.Tasks.Task<int> BatchAsync(global::System.Threading.CancellationToken ct)
    {
        try
        {
            var _result = await _inner.BatchAsync(ct);
            _batch_items.Add(_result);
            return _result;
        }
        catch (Exception)
        {
            throw;
        }
    }

    public async global::System.Threading.Tasks.Task<int?> MaybeAsync(global::System.Threading.CancellationToken ct)
    {
        try
        {
            var _result = await _inner.MaybeAsync(ct);
            var _tagged = _result;
            if (_tagged is { } _read0)
                _maybe_items.Add(_read0);
            return _result;
        }
        catch (Exception)
        {
            throw;
        }
    }
```

If any accepted snapshot differs from the text above, the implementation is wrong. Fix it; do not re-accept.

- [ ] **Step 10: Run the generator suite, then the solution**

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generator.Tests/ZeroAlloc.Telemetry.Generator.Tests.csproj`

Expected: `Passed!  - Failed:     0`, including `ResultMetricsProbe_Compiles`, which proves every shape compiles with nullable enabled and no warning in the generated file. `git status --short tests/` shows no change to any pre-existing snapshot.

Run: `dotnet test ZeroAlloc.Telemetry.slnx -c Release`

Expected: `Failed: 0` in every project, and `0 Warning(s)`.

- [ ] **Step 11: Commit**

```bash
git add src/ZeroAlloc.Telemetry.Generator tests/ZeroAlloc.Telemetry.Generator.Tests
git commit -F - <<'EOF'
feat(generator): record metrics from the return value and guard Count and Histogram

CountFromResult adds a member of the awaited return value to a Counter<long>, and
HistogramFromResult records one in a Histogram<double>. Both read paths with the same resolver
and null rules as TraceTagFromResult. A null value is skipped, a decimal is cast to double, and
a When guard stops the member being read at all. They sit with the result tags inside the try
and share its null-state copy, and they need no [Trace].

When on Count and Histogram records only when the guard holds. A guarded histogram records
nothing on a throw, since there is no result to evaluate; an unguarded one records as before.

ZTEL009 reports a member that does not fit its instrument, and ZTEL005 now also covers the new
attributes and When on a method with no return value.

Closes #142

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 6: Generator-backed runtime tests and the AOT smoke sample

**Files:**
- Create: `tests/ZeroAlloc.Telemetry.Generated.Tests/ZeroAlloc.Telemetry.Generated.Tests.csproj`
- Create: `tests/ZeroAlloc.Telemetry.Generated.Tests/TokenUsage.cs`
- Create: `tests/ZeroAlloc.Telemetry.Generated.Tests/ChatResult.cs`
- Create: `tests/ZeroAlloc.Telemetry.Generated.Tests/IChatService.cs`
- Create: `tests/ZeroAlloc.Telemetry.Generated.Tests/FakeChatService.cs`
- Create: `tests/ZeroAlloc.Telemetry.Generated.Tests/MetricCapture.cs`
- Create: `tests/ZeroAlloc.Telemetry.Generated.Tests/ResultMetricsBehaviorTests.cs`
- Modify: `ZeroAlloc.Telemetry.slnx`
- Create: `samples/ZeroAlloc.Telemetry.AotSmoke/OrderReceipt.cs`
- Modify: `samples/ZeroAlloc.Telemetry.AotSmoke/IOrderService.cs`, `OrderService.cs`, `Program.cs` and `ZeroAlloc.Telemetry.AotSmoke.csproj`

**Interfaces:**
- Consumes: the generator's emitted `ChatServiceInstrumented`, the proxy for `IChatService`.
- Produces: the test project `ZeroAlloc.Telemetry.Generated.Tests`, which CI runs through the solution.

**CI wiring.** The `build` job in `.github/workflows/ci.yml` runs `dotnet test ZeroAlloc.Telemetry.slnx`, so adding the project to the solution is the CI change. `ci.yml` itself is not edited. The existing `ZeroAlloc.Telemetry.Tests` cannot host these tests: it declares private nested `[Instrument]` interfaces with hand-written proxies, which the generator would try to proxy as well.

- [ ] **Step 1: Create the project and add it to the solution**

`tests/ZeroAlloc.Telemetry.Generated.Tests/ZeroAlloc.Telemetry.Generated.Tests.csproj`:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.10.1" />
    <PackageReference Include="xunit" Version="2.9.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="4.0.0" PrivateAssets="all" />
    <PackageReference Include="AwesomeAssertions" Version="9.6.0" />
    <ProjectReference Include="..\..\src\ZeroAlloc.Telemetry\ZeroAlloc.Telemetry.csproj" />
    <!-- The attribute project's own generator reference is PrivateAssets and does not flow, so
         the proxies here come from this reference, as they would for a consumer. -->
    <ProjectReference Include="..\..\src\ZeroAlloc.Telemetry.Generator\ZeroAlloc.Telemetry.Generator.csproj"
                      OutputItemType="Analyzer"
                      ReferenceOutputAssembly="false" />
  </ItemGroup>
  <ItemGroup>
    <Using Include="Xunit" />
    <Using Include="AwesomeAssertions" />
  </ItemGroup>
</Project>
```

In `ZeroAlloc.Telemetry.slnx`, add inside `<Folder Name="/tests/">`:

```xml
    <Project Path="tests/ZeroAlloc.Telemetry.Generated.Tests/ZeroAlloc.Telemetry.Generated.Tests.csproj" />
```

- [ ] **Step 2: Add the types under test**

Each type goes in its own file, because MA0048 requires one type per file.

`TokenUsage.cs`:

```csharp
namespace ZeroAlloc.Telemetry.Generated.Tests;

public sealed class TokenUsage
{
    public int Input { get; init; }

    public long? Output { get; init; }

    public decimal Cost { get; init; }
}
```

`ChatResult.cs`:

```csharp
namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// A Result-shaped struct whose <see cref="Value"/> throws on failure. If the proxy read a guarded
/// member on a failed result, the call itself would throw, so these tests prove the guard stops
/// the read, not merely the recording.
/// </summary>
public readonly struct ChatResult
{
    private readonly TokenUsage? _value;

    private ChatResult(bool isSuccess, TokenUsage? value)
    {
        IsSuccess = isSuccess;
        _value = value;
    }

    public bool IsSuccess { get; }

    public TokenUsage Value => IsSuccess
        ? _value!
        : throw new InvalidOperationException("A failed result has no value.");

    public static ChatResult Success(TokenUsage value) => new(isSuccess: true, value);

    public static ChatResult Failure() => new(isSuccess: false, value: null);
}
```

`IChatService.cs`:

```csharp
namespace ZeroAlloc.Telemetry.Generated.Tests;

// The name is a literal because attribute arguments on a type are bound outside that type, so
// a constant declared inside the interface is not in scope here. ResultMetricsBehaviorTests
// repeats it as its MeterName.
[Instrument("ZeroAlloc.Telemetry.Generated.Tests.Chat")]
public interface IChatService
{
    [Count("chat.completions", When = "IsSuccess", Unit = "{completion}", Description = "Successful completions")]
    [Histogram("chat.duration", When = "IsSuccess", Unit = "ms")]
    [CountFromResult("chat.tokens.input", "Value.Input", When = "IsSuccess", Unit = "{token}", Description = "Prompt tokens consumed")]
    [CountFromResult("chat.tokens.output", "Value.Output", When = "IsSuccess", Unit = "{token}")]
    [HistogramFromResult("chat.cost", "Value.Cost", When = "IsSuccess", Unit = "USD")]
    Task<ChatResult> CompleteAsync(string prompt, CancellationToken ct);

    [HistogramFromResult("chat.score", "Value")]
    Task<double?> ScoreAsync(CancellationToken ct);
}
```

`FakeChatService.cs`:

```csharp
namespace ZeroAlloc.Telemetry.Generated.Tests;

public sealed class FakeChatService : IChatService
{
    public ChatResult Result { get; init; } = ChatResult.Failure();

    public double? Score { get; init; }

    public bool Throw { get; init; }

    public Task<ChatResult> CompleteAsync(string prompt, CancellationToken ct) =>
        Throw
            ? Task.FromException<ChatResult>(new InvalidOperationException("boom"))
            : Task.FromResult(Result);

    public Task<double?> ScoreAsync(CancellationToken ct) => Task.FromResult(Score);
}
```

`MetricCapture.cs`:

```csharp
using System.Diagnostics.Metrics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>Records every measurement published by one meter, by instrument name.</summary>
public sealed class MetricCapture : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly Lock _gate = new();
    private readonly List<KeyValuePair<string, double>> _measurements = [];
    private readonly Dictionary<string, Instrument> _instruments = new(StringComparer.Ordinal);

    public MetricCapture(string meterName)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (!string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal))
                return;

            lock (_gate)
                _instruments[instrument.Name] = instrument;

            listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => Add(instrument.Name, value));
        _listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => Add(instrument.Name, value));
        _listener.Start();
    }

    public IReadOnlyList<double> ValuesOf(string instrument)
    {
        lock (_gate)
        {
            return _measurements
                .Where(m => string.Equals(m.Key, instrument, StringComparison.Ordinal))
                .Select(m => m.Value)
                .ToList();
        }
    }

    public Instrument Published(string instrument)
    {
        lock (_gate)
            return _instruments[instrument];
    }

    public void Dispose() => _listener.Dispose();

    private void Add(string instrument, double value)
    {
        lock (_gate)
            _measurements.Add(new KeyValuePair<string, double>(instrument, value));
    }
}
```

- [ ] **Step 3: Write the behaviour tests**

`ResultMetricsBehaviorTests.cs`:

```csharp
namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Runs the generator's own proxy, not a hand-written copy, and observes it with a
/// <see cref="System.Diagnostics.Metrics.MeterListener"/>. xUnit creates a new instance per test,
/// so each test gets its own capture. The class is the only user of its meter, so its tests run
/// sequentially and in isolation.
/// </summary>
public sealed class ResultMetricsBehaviorTests : IDisposable
{
    private const string MeterName = "ZeroAlloc.Telemetry.Generated.Tests.Chat";

    private readonly MetricCapture _capture = new(MeterName);

    public void Dispose() => _capture.Dispose();

    [Fact]
    public async Task Success_RecordsTheValuesCarriedByTheResult()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService
        {
            Result = ChatResult.Success(new TokenUsage { Input = 120, Output = 45, Cost = 0.25m }),
        });

        await proxy.CompleteAsync("hi", CancellationToken.None).ConfigureAwait(true);

        _capture.ValuesOf("chat.tokens.input").Should().Equal(120d);
        _capture.ValuesOf("chat.tokens.output").Should().Equal(45d);
        _capture.ValuesOf("chat.cost").Should().Equal(0.25d);
        _capture.ValuesOf("chat.completions").Should().Equal(1d);
        _capture.ValuesOf("chat.duration").Should().ContainSingle().Which.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task FailedResult_RecordsNothing_AndIsNotCounted()
    {
        // ChatResult.Value throws on failure, so reaching the assertions also proves no guarded
        // member was read.
        var proxy = new ChatServiceInstrumented(new FakeChatService { Result = ChatResult.Failure() });

        await proxy.CompleteAsync("hi", CancellationToken.None).ConfigureAwait(true);

        _capture.ValuesOf("chat.completions").Should().BeEmpty();
        _capture.ValuesOf("chat.duration").Should().BeEmpty();
        _capture.ValuesOf("chat.tokens.input").Should().BeEmpty();
        _capture.ValuesOf("chat.tokens.output").Should().BeEmpty();
        _capture.ValuesOf("chat.cost").Should().BeEmpty();
    }

    [Fact]
    public async Task NullMember_IsNotRecorded()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService
        {
            Result = ChatResult.Success(new TokenUsage { Input = 7, Output = null }),
        });

        await proxy.CompleteAsync("hi", CancellationToken.None).ConfigureAwait(true);

        _capture.ValuesOf("chat.tokens.input").Should().Equal(7d);
        _capture.ValuesOf("chat.tokens.output").Should().BeEmpty();
    }

    [Fact]
    public async Task NullReturnValue_IsNotRecorded()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService { Score = null });

        await proxy.ScoreAsync(CancellationToken.None).ConfigureAwait(true);

        _capture.ValuesOf("chat.score").Should().BeEmpty();
    }

    [Fact]
    public async Task NullableReturnValue_IsRecorded_WhenPresent()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService { Score = 0.9 });

        await proxy.ScoreAsync(CancellationToken.None).ConfigureAwait(true);

        _capture.ValuesOf("chat.score").Should().Equal(0.9d);
    }

    [Fact]
    public async Task Throw_RecordsNothing_ForGuardedInstruments()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService { Throw = true });

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => proxy.CompleteAsync("hi", CancellationToken.None)).ConfigureAwait(true);

        _capture.ValuesOf("chat.duration").Should().BeEmpty();
        _capture.ValuesOf("chat.completions").Should().BeEmpty();
    }

    [Fact]
    public async Task UnitAndDescription_ReachThePublishedInstrument()
    {
        var proxy = new ChatServiceInstrumented(new FakeChatService
        {
            Result = ChatResult.Success(new TokenUsage { Input = 1 }),
        });

        await proxy.CompleteAsync("hi", CancellationToken.None).ConfigureAwait(true);

        _capture.Published("chat.tokens.input").Unit.Should().Be("{token}");
        _capture.Published("chat.tokens.input").Description.Should().Be("Prompt tokens consumed");
        _capture.Published("chat.completions").Unit.Should().Be("{completion}");
        _capture.Published("chat.completions").Description.Should().Be("Successful completions");
        _capture.Published("chat.cost").Unit.Should().Be("USD");
        _capture.Published("chat.duration").Unit.Should().Be("ms");
    }
}
```

- [ ] **Step 4: Run the new project**

Run: `dotnet test tests/ZeroAlloc.Telemetry.Generated.Tests/ZeroAlloc.Telemetry.Generated.Tests.csproj -c Release`

Expected: `Passed!  - Failed:     0, Passed:     7`.

These tests exercise code Task 5 already made green, so they pass on first run. To confirm they can fail, temporarily change `When = "IsSuccess"` on `[Count("chat.completions", ...)]` to nothing, rerun, and see `FailedResult_RecordsNothing_AndIsNotCounted` fail with `Expected ... to be empty, but found {1.0}`. Then restore the attribute.

If an analyzer fires on the generated `ChatServiceInstrumented.g.cs`, that is the generator emitting code a consumer cannot build. Fix `ProxyWriter`, add the shape to `GeneratedCodeCompilesTests`, and do not suppress.

- [ ] **Step 5: Extend the AOT smoke sample**

Create `samples/ZeroAlloc.Telemetry.AotSmoke/OrderReceipt.cs`:

```csharp
namespace ZeroAlloc.Telemetry.AotSmoke;

public sealed class OrderReceipt
{
    public int Lines { get; init; }
}
```

In `IOrderService.cs`, add:

```csharp
    [CountFromResult("orders.lines", "Lines", Unit = "{line}")]
    ValueTask<OrderReceipt> ReceiptAsync(string customerId, CancellationToken ct);
```

In `OrderService.cs`, add:

```csharp
    public ValueTask<OrderReceipt> ReceiptAsync(string customerId, CancellationToken ct) =>
        ValueTask.FromResult(new OrderReceipt { Lines = 3 });
```

Replace `Program.cs`:

```csharp
using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.Telemetry.AotSmoke;

// Exercise the generator-emitted OrderServiceInstrumented proxy (strips
// leading 'I', appends 'Instrumented') under PublishAot=true. The proxy
// wraps every method in Activity + Meter calls — this verifies that whole
// chain compiles and runs AOT-safely.

var impl = new OrderService();
var proxy = new OrderServiceInstrumented(impl);

var id = await proxy.CreateAsync("cust-1", CancellationToken.None).ConfigureAwait(false);
if (id != 42) return Fail($"CreateAsync expected 42, got {id}");
if (impl.CallCount != 1) return Fail($"Inner call count expected 1, got {impl.CallCount}");

// Multiple invocations — each should reach the inner
for (var i = 0; i < 3; i++)
{
    _ = await proxy.CreateAsync("cust", CancellationToken.None).ConfigureAwait(false);
}
if (impl.CallCount != 4) return Fail($"After 4 total invocations, CallCount expected 4, got {impl.CallCount}");

// A result-driven counter: the value is read from the returned receipt, not a constant 1.
long lines = 0;
using var listener = new MeterListener();
listener.InstrumentPublished = (instrument, l) =>
{
    if (string.Equals(instrument.Meter.Name, "ZeroAlloc.Telemetry.AotSmoke", StringComparison.Ordinal)
        && string.Equals(instrument.Name, "orders.lines", StringComparison.Ordinal))
    {
        l.EnableMeasurementEvents(instrument);
    }
};
listener.SetMeasurementEventCallback<long>((_, value, _, _) => lines += value);
listener.Start();

var receipt = await proxy.ReceiptAsync("cust-1", CancellationToken.None).ConfigureAwait(false);
if (receipt.Lines != 3) return Fail($"ReceiptAsync expected 3 lines, got {receipt.Lines}");
if (lines != 3) return Fail($"orders.lines expected 3, got {lines}");

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}
```

In `ZeroAlloc.Telemetry.AotSmoke.csproj`, delete the `<NoWarn>$(NoWarn);EPC12</NoWarn>` line and the comment above it. The generated file carries its own `#pragma warning disable EPC12`, and since Task 2 the only catch that reads `_ex` is the traced one, so the project-wide suppression is dead weight.

- [ ] **Step 6: Build and run the sample**

Run: `dotnet build samples/ZeroAlloc.Telemetry.AotSmoke/ZeroAlloc.Telemetry.AotSmoke.csproj -c Release && dotnet run --project samples/ZeroAlloc.Telemetry.AotSmoke/ZeroAlloc.Telemetry.AotSmoke.csproj -c Release --no-build`

Expected: `Build succeeded.` with `0 Warning(s)`, then `AOT smoke: PASS`.

If EPC12 fires without the `NoWarn`, the file-scoped pragma in `ProxyWriter.Write` is not reaching the analyzer. Investigate that; do not restore the `NoWarn`.

The native publish runs in the CI `aot-smoke` job on Linux. Locally on Windows, run `dotnet publish samples/ZeroAlloc.Telemetry.AotSmoke/ZeroAlloc.Telemetry.AotSmoke.csproj -r win-x64 -c Release -o ./aot-out` and then `./aot-out/ZeroAlloc.Telemetry.AotSmoke.exe`. That needs the C++ build tools. Expected: no IL2026/IL3050 warnings, then `AOT smoke: PASS`. Delete `./aot-out` afterwards.

- [ ] **Step 7: Run the solution**

Run: `dotnet test ZeroAlloc.Telemetry.slnx -c Release`

Expected: four test assemblies run, including `ZeroAlloc.Telemetry.Generated.Tests.dll`, all with `Failed: 0`.

- [ ] **Step 8: Commit**

```bash
git add ZeroAlloc.Telemetry.slnx tests/ZeroAlloc.Telemetry.Generated.Tests samples/ZeroAlloc.Telemetry.AotSmoke
git commit -F - <<'EOF'
test: run generated proxies against a MeterListener, and in the AOT sample

The runtime tests used hand-written copies of the proxy, so they could not catch a generator
regression. A new project references the generator as an analyzer, as a consumer would, and
asserts the recorded values, that a false guard or a null member records nothing, that a failed
Result is not counted, and that Unit and Description reach the published instrument.

The AOT smoke sample gains a result-driven counter, and loses a NoWarn for EPC12 that the
generated file's own pragma already covers.

Refs #142

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

---

### Task 7: Docs, the doc-example fix, follow-up issues, and the PR

**Files:**
- Modify: `src/ZeroAlloc.Telemetry/InstrumentAttribute.cs:9-20`
- Modify: `docs/attributes.md`, `docs/source-generator.md`, `docs/index.md:22` and `README.md`

**Interfaces:**
- Consumes: every name above. The docs quote emitted code that must match the snapshots from Task 5.
- Produces: nothing for later tasks.

- [ ] **Step 1: Fix the `InstrumentAttribute` example**

`Metric`, `Name` and `ActivitySource` are get-only, so the current example's named arguments do not compile. In `InstrumentAttribute.cs`, replace lines 9-20:

```csharp
/// <example>
/// <code>
/// [Instrument("MyApp.Orders")]
/// public interface IOrderService
/// {
///     [Trace("order.create")]
///     [Count("orders.created")]
///     ValueTask&lt;OrderId&gt; CreateOrderAsync(CreateOrderRequest request, CancellationToken ct);
/// }
/// // Generator emits: OrderServiceInstrumented : IOrderService
/// </code>
/// </example>
```

Verify: `grep -nE '\[(Instrument|Trace|Count|Histogram)\((ActivitySource|Name|Metric) =' -r src docs README.md` prints nothing.

Run: `dotnet build src/ZeroAlloc.Telemetry/ZeroAlloc.Telemetry.csproj -c Release`, expecting `0 Warning(s)`.

```bash
git add src/ZeroAlloc.Telemetry/InstrumentAttribute.cs
git commit -F - <<'EOF'
fix(core): use constructor arguments in the InstrumentAttribute example

The example set ActivitySource, Name and Metric as named arguments, but all three are get-only
properties filled by the constructor, so the example did not compile.

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 2: Update `docs/attributes.md`**

Change the front-matter `description` to:

```text
description: Reference for [Instrument], [Trace], [Count], [Histogram], [CountFromResult], [HistogramFromResult], [TraceTag], [TraceTagFromResult], and [TraceTagConstant] — the attributes in ZeroAlloc.Telemetry.
```

Replace the whole `## [Count]` section, lines 146-175, with:

````markdown
## [Count]

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class CountAttribute : Attribute
{
    public string Metric { get; }
    public string? When { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public CountAttribute(string metric);
}
```

**Placement:** Interface method.

**Effect:** Increments a `Counter<long>` by 1 after a successful (non-throwing) call only.

The counter field is a static field on the proxy — one per metric name and instrument kind across all methods. If two methods share `[Count("x")]`, only one `Counter<long>` field is emitted. A `[Histogram("x")]` gets its own field.

```csharp
[Count("payments.charged")]
ValueTask<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct);
```

Generated field + increment:
```csharp
private static readonly Counter<long> _payments_charged =
    _meter.CreateCounter<long>("payments.charged");

// In the method body (success path only):
_payments_charged.Add(1);
```

### Counting only real successes

A method returning a `Result<T, E>` returns normally when it fails, so a plain `[Count]` counts failures as successes. `When` names a boolean member of the return value that must be true. It is the same guard as [`[TraceTagFromResult]`](#tagging-only-on-one-branch), described under [Member paths and When](#member-paths-and-when):

```csharp
[Count("orders.accepted", When = "IsSuccess")]
Task<Result<OrderId, OrderError>> AcceptAsync(Order order, CancellationToken ct);
```

```csharp
// Generated, for a Result that is a class:
var _tagged = _result;
if (_tagged?.IsSuccess == true)
    _orders_accepted.Add(1);
```

`When` needs a return value. On a method returning `void`, `Task` or `ValueTask` the generator reports **ZTEL005** and the counter records nothing, rather than counting the very calls the guard was meant to exclude.

### Unit and description

`Unit` and `Description` pass through to `Meter.CreateCounter`, and are only emitted when set:

```csharp
[Count("orders.created", Unit = "{order}", Description = "Orders accepted for fulfilment")]
// → _meter.CreateCounter<long>("orders.created", unit: "{order}", description: "Orders accepted for fulfilment");
```

Attributes that share a metric name and kind share one instrument. The first `Unit` and the first `Description` set on any of them, in declaration order, are the ones used, so declare each once.
````

Replace the whole `## [Histogram]` section, lines 179-219, with:

````markdown
## [Histogram]

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class HistogramAttribute : Attribute
{
    public string Metric { get; }
    public string? When { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public HistogramAttribute(string metric);
}
```

**Placement:** Interface method.

**Effect:** Records the elapsed time in milliseconds in a `Histogram<double>` on every call — including when the method throws.

Uses `Stopwatch.GetTimestamp()` before the call and `Stopwatch.GetElapsedTime(ts).TotalMilliseconds` after, so the measurement includes the full method duration regardless of outcome.

```csharp
[Histogram("payment.charge_ms", Unit = "ms")]
ValueTask<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct);
```

Generated field + recording:
```csharp
private static readonly Histogram<double> _payment_charge_ms =
    _meter.CreateHistogram<double>("payment.charge_ms", unit: "ms");

// In the method body:
var _sw = Stopwatch.GetTimestamp();
try
{
    var _result = await _inner.ChargeAsync(request, ct);
    _payment_charge_ms.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);
    return _result;
}
catch (Exception)
{
    _payment_charge_ms.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);
    throw;
}
```

### Timing only successful calls

With `When`, the duration is recorded only on a non-throwing return whose guard is true. **A call that throws records nothing**: there is no result to evaluate the guard against, and guessing either way would put the wrong calls in the distribution. An unguarded histogram still records on both paths.

```csharp
[Trace("orders.accept")]
[Histogram("orders.accept_ms", When = "IsSuccess", Unit = "ms")]
Task<Result<string, OrderError>> AcceptAsync(string orderId, CancellationToken ct);
```

```csharp
// Generated:
try
{
    var _result = await _inner.AcceptAsync(orderId, ct);
    var _tagged = _result;
    if (_tagged?.IsSuccess == true)
        _orders_accept_ms.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);
    return _result;
}
catch (Exception _ex)
{
    _activity?.SetStatus(ActivityStatusCode.Error, _ex.Message);
    throw;   // no Record: a guarded histogram has nothing to evaluate on a throw
}
```

`Unit` and `Description` work as on [`[Count]`](#unit-and-description).

---

## [CountFromResult]

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CountFromResultAttribute : Attribute
{
    public string Metric { get; }
    public string Member { get; }
    public string? When { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public CountFromResultAttribute(string metric, string member);
}
```

**Placement:** Method. Does **not** need `[Trace]`: it is a metric, not span data. May be applied more than once.

**Effect:** After a successful (non-throwing) call, adds the value of `member` to a `Counter<long>`. For values only known once the call returns: tokens consumed, rows written, items returned.

```csharp
[CountFromResult("llm.tokens.input", "Value.Usage.InputTokens", When = "IsSuccess", Unit = "{token}")]
[CountFromResult("llm.tokens.output", "Value.Usage.OutputTokens", When = "IsSuccess", Unit = "{token}")]
ValueTask<Result<ChatResponse, ChatError>> CompleteAsync(ChatRequest request, CancellationToken ct);
```

```csharp
// Generated, for a Result that is a struct and a ChatResponse and Usage that are classes:
var _result = await _inner.CompleteAsync(request, ct);
if (_result.IsSuccess && _result.Value?.Usage?.InputTokens is { } _read0)
    _llm_tokens_input.Add(_read0);
if (_result.IsSuccess && _result.Value?.Usage?.OutputTokens is { } _read1)
    _llm_tokens_output.Add(_read1);
```

- **Member type.** It must convert implicitly to `long`: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint` or `long`, or a nullable form of one. Anything else, including `ulong`, `char`, enums and floating-point types, is **ZTEL009**.
- **Null.** A null value, or a null anywhere along the path, is not recorded.
- **Counters only go up.** A negative value is added as it is. Backends expect a counter to be monotonic, and may reject a decrease or read it as a reset, so count quantities that cannot go negative.
- **An empty member** adds the return value itself: `[CountFromResult("batch.items", "")]` on `Task<int>`.
- Each metric name gets its own counter, so the two above are separate instruments.

---

## [HistogramFromResult]

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class HistogramFromResultAttribute : Attribute
{
    public string Metric { get; }
    public string Member { get; }
    public string? When { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public HistogramFromResultAttribute(string metric, string member);
}
```

**Placement:** Method. Does not need `[Trace]`. May be applied more than once.

**Effect:** After a successful (non-throwing) call, records the value of `member` in a `Histogram<double>`. For distributions known only afterwards: a confidence score, a result size, a cost.

```csharp
[HistogramFromResult("answer.confidence", "Value.Confidence", When = "IsSuccess", Unit = "1")]
[HistogramFromResult("answer.cost", "Value.Cost", When = "IsSuccess", Unit = "USD")]
Task<Result<Answer, AskError>> AskAsync(Question question, CancellationToken ct);
```

```csharp
// Generated, for a Result and an Answer that are classes, with a double Confidence and a decimal Cost:
var _tagged = _result;
if (_tagged?.IsSuccess == true && _tagged?.Value?.Confidence is { } _read0)
    _answer_confidence.Record(_read0);
if (_tagged?.IsSuccess == true && _tagged?.Value?.Cost is { } _read1)
    _answer_cost.Record((double)_read1);
```

- **Member type.** It must be numeric: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double` or `decimal`, or a nullable form of one. Anything else is **ZTEL009**.
- **`decimal`** has no implicit conversion to `double`, so it is converted with an explicit `(double)` cast.
- **Null** values are not recorded, and **a call that throws** records nothing, since there is no result.

---

## Member paths and When

`[TraceTagFromResult]`, `[CountFromResult]`, `[HistogramFromResult]` and every `When` share one path resolver.

- **Paths start at the awaited return value.** For `Task<Result<T, E>>` that is the `Result`, so the success value is reached through `Value`, as in `Value.Usage.InputTokens`.
- **Each segment** names a property or field of the type reached so far, including members inherited from base types and interfaces.
- **The operator for each step** is chosen from the type: `?.` where the value can be null, `.` where it cannot. A `Value` segment on a nullable value type is dropped, since `?.` already unwraps it.
- **`When`** is a path to a `bool` or `bool?` member that must be `true`. The guard runs first and short-circuits, so the member is **not read at all** when it is false. That matters for a `Result` whose `Value` throws when unset.
- **A segment that names nothing** is **ZTEL007**. It is reported at the argument, naming the segment and the type it was looked up on. A `When` that resolves to anything other than `bool` or `bool?` is **ZTEL008**. Both are errors, and neither attribute is emitted, so the diagnostic is the only error.
- **On a method with no return value** (`void`, `Task`, `ValueTask`), the result-reading attributes and `When` on `[Count]`/`[Histogram]` report **ZTEL005** and record nothing.

**Where the reads happen.** After the inner call, inside the same `try`, the result tags come first, then the result-driven instruments, then `[Count]` and `[Histogram]`. When the result can be null, all of them read one copy, `_tagged`. Null-testing `_result` itself would leave it maybe-null for the `return _result;` that follows, and that raises CS8603 in consumers with nullable warnings on.
````

In `## [TraceTagFromResult]`, after the paragraph ending `...the generator reports **ZTEL005**.`, add:

```markdown
A misspelt member or `When` is reported as **ZTEL007**, and a `When` that is not a boolean as **ZTEL008** — see [Member paths and When](#member-paths-and-when).
```

In `## Methods Without Attributes`, replace the first sentence with:

```markdown
Methods with no `[Trace]`, `[Count]`, `[Histogram]`, `[CountFromResult]` or `[HistogramFromResult]` annotation are passed through to the inner implementation without any wrapping — no try/catch, no timing, no span.
```

- [ ] **Step 3: Update `docs/source-generator.md`**

Replace line 79, the dedup sentence, with:

```markdown
Static metric fields are keyed by instrument kind and metric name. If two methods share `[Count("orders.created")]`, or a `[Count]` and a `[CountFromResult]` use the same name, only one `Counter<long>` field is emitted. A `[Histogram]` with the same name gets its own `Histogram<double>` field.
```

Before `## Field Name Derivation`, add:

````markdown
### Result-driven instruments

Input:
```csharp
[CountFromResult("llm.tokens.input", "Value.Input", When = "IsSuccess", Unit = "{token}")]
[HistogramFromResult("llm.cost", "Value.Cost", When = "IsSuccess", Unit = "USD")]
Task<Result<TokenUsage, LlmError>> CompleteAsync(string prompt, CancellationToken ct);
```

Output:
```csharp
private static readonly Counter<long> _llm_tokens_input = _meter.CreateCounter<long>("llm.tokens.input", unit: "{token}");
private static readonly Histogram<double> _llm_cost = _meter.CreateHistogram<double>("llm.cost", unit: "USD");

public async Task<Result<TokenUsage, LlmError>> CompleteAsync(string prompt, CancellationToken ct)
{
    try
    {
        var _result = await _inner.CompleteAsync(prompt, ct);
        var _tagged = _result;
        if (_tagged?.IsSuccess == true && _tagged?.Value?.Input is { } _read0)
            _llm_tokens_input.Add(_read0);
        if (_tagged?.IsSuccess == true && _tagged?.Value?.Cost is { } _read1)
            _llm_cost.Record((double)_read1);
        return _result;
    }
    catch (Exception)
    {
        throw;
    }
}
```

The catch only declares the exception variable when a `[Trace]` span reads it.
````

Replace the `## Field Name Derivation` section with:

```markdown
## Field Name Derivation

A field name is `_` plus the metric name, with every character that is not a letter, digit or underscore replaced by `_`:

| Metric name | Field name |
|---|---|
| `orders.created` | `_orders_created` |
| `order.get_ms` | `_order_get_ms` |
| `payment.charge-duration` | `_payment_charge_duration` |
| `http/requests total` | `_http_requests_total` |

Names that would still collide are made distinct with a numeric suffix, in declaration order. `a.b` then `a_b` give `_a_b` and `_a_b_2`, and a `[Count("x")]` and a `[Histogram("x")]` give `_x` and `_x_2`. A name that would clash with the proxy's own members or locals, such as `meter` or `result`, is prefixed instead: `_metric_meter`, `_metric_result`. Metric, span and tag names are emitted as escaped string literals, so any character is safe.
```

Before `## Release tracking`, add:

```markdown
## Diagnostics

| ID | Severity | Reported when |
|---|---|---|
| ZTEL001 | Error | `[Instrument]` is on a class, struct or record instead of an interface |
| ZTEL002 | Error | `[Instrument]` has an empty or whitespace name |
| ZTEL003 | Warning | `[Trace]`, `[Count]`, `[Histogram]`, `[CountFromResult]` or `[HistogramFromResult]` is on a method of a type without `[Instrument]`, so no proxy is generated |
| ZTEL004 | Warning | `[TraceTag]`, `[TraceTagFromResult]` or `[TraceTagConstant]` is on a method without `[Trace]` |
| ZTEL005 | Warning | `[TraceTagFromResult]`, `[CountFromResult]`, `[HistogramFromResult]`, or `When` on `[Count]`/`[Histogram]`, is on a method returning `void`, `Task` or `ValueTask`. The message names the attribute; nothing is recorded |
| ZTEL006 | Warning | A `[Trace]` name contains a `{token}` other than `{type}` |
| ZTEL007 | Error | A segment of a member path or `When` names no property or field of the type reached so far. Reported at the argument, naming the segment and the type |
| ZTEL008 | Error | `When` resolves to a member that is not `bool` or `bool?` |
| ZTEL009 | Error | `[CountFromResult]`'s member does not convert implicitly to `long`, or `[HistogramFromResult]`'s member is not numeric |

ZTEL007 and ZTEL008 replace what used to be a compile error inside the generated proxy, so they do not fail any build that used to succeed.
```

In `docs/index.md` line 22, replace the attribute cell with:

```markdown
| [Attribute Reference](attributes.md) | `[Instrument]`, `[Trace]`, `[Count]`, `[Histogram]`, `[CountFromResult]`, `[HistogramFromResult]`, `[TraceTag]`, `[TraceTagFromResult]`, `[TraceTagConstant]` |
```

- [ ] **Step 4: Update `README.md`**

Replace the `## Instruments` table with:

```markdown
| Attribute | Instrument | Recorded when |
|---|---|---|
| `[Trace("name")]` | `ActivitySource.StartActivity("name")` | Every call — stopped in `finally`, Error status on exception |
| `[Count("metric")]` | `Counter<long>.Add(1)` | After a successful (non-throwing) call only |
| `[Histogram("metric")]` | `Histogram<double>.Record(ms)` | Every call including on exception |
| `[CountFromResult("metric", "Member")]` | `Counter<long>.Add(value)` | After a successful call, when the member is not null |
| `[HistogramFromResult("metric", "Member")]` | `Histogram<double>.Record(value)` | After a successful call, when the member is not null |

**Metrics from the result.** `[CountFromResult]` and `[HistogramFromResult]` record a value carried by the return value, such as tokens consumed or a confidence score. `When = "IsSuccess"` on any metric records it only when the result says the call succeeded, so a failed `Result<T, E>` is no longer counted as a success. `Unit` and `Description` pass through to the instrument.
```

In the Documentation table, change the Attribute Reference description to list `[CountFromResult]` and `[HistogramFromResult]` as well, in the same order as `docs/index.md`.

- [ ] **Step 5: Check the docs against the generator**

Run: `grep -n "catch (Exception _ex)" docs/source-generator.md README.md docs/attributes.md`

Every hit must be in an example whose method has `[Trace]`. The only README example, `CreateOrderAsync`, has one, so it stays. Fix any hit that belongs to a method without `[Trace]` by writing `catch (Exception)`.

Run: `dotnet test ZeroAlloc.Telemetry.slnx -c Release`, expecting every project at `Failed: 0`. This is the final green check before the PR.

- [ ] **Step 6: Commit the docs**

```bash
git add docs README.md
git commit -F - <<'EOF'
docs: document metrics from the result, When on Count and Histogram, and ZTEL007 to ZTEL009

attributes.md covers the two new attributes, When, Unit and Description, member paths, the
type rules, that counters only go up, and that a guarded histogram records nothing on a throw.
source-generator.md gains a diagnostics table and the new field naming rules.

Refs #142

Co-Authored-By: Claude Opus 5.5 <noreply@anthropic.com>
EOF
```

- [ ] **Step 7: File the follow-up issues, after checking for existing ones**

Run:

```bash
gh issue list --repo ZeroAlloc-Net/ZeroAlloc.Telemetry --state open --search "MetricTagFromResult in:title,body"
gh issue list --repo ZeroAlloc-Net/ZeroAlloc.Telemetry --state open --search "incremental equatable in:title,body"
gh issue list --repo ZeroAlloc-Net/ZeroAlloc.Telemetry --state open --search "TraceTag unresolved member OR ZTEL003 in:title,body"
```

For any search that returns a matching open issue, add a comment with `gh issue comment <number> --body-file -` using the body below, instead of creating a new issue. Otherwise:

```bash
gh issue create --repo ZeroAlloc-Net/ZeroAlloc.Telemetry \
  --title "[MetricTagFromResult]: dimensions read from the return value" --body-file - <<'EOF'
**Summary**
`[CountFromResult]` and `[HistogramFromResult]` from #142 record a value from the result, but
cannot add a dimension read from it, such as the versioned model id at `Value.Model`. The #142
spec deferred it to this issue.

**Proposal**
`[MetricTagFromResult("gen_ai.response.model", "Value.Model")]` on the method adds a tag to every
metric the method records. It reuses `PathResolver` and `When`, as the other result attributes do.

**Open questions**
- Does the tag apply to every instrument on the method, or is it keyed to one metric name?
- Tag values box. Is a `TagList` built only when an instrument is enabled, to keep the
  zero-alloc path when no listener is attached?

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
```

```bash
gh issue create --repo ZeroAlloc-Net/ZeroAlloc.Telemetry \
  --title "Generator models are not value-equatable, so incremental caching never hits" --body-file - <<'EOF'
**What is wrong**
The pipeline output is `ParseResult`, which holds `InstrumentModel` and an
`ImmutableArray<Diagnostic>`. At 1.6.4:
- https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/87290e066c299a41fbaf3aea894d456fcbbdc494/src/ZeroAlloc.Telemetry.Generator/InstrumentGenerator.cs#L712
- https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/87290e066c299a41fbaf3aea894d456fcbbdc494/src/ZeroAlloc.Telemetry.Generator/Models/InstrumentModel.cs#L8
- https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/87290e066c299a41fbaf3aea894d456fcbbdc494/src/ZeroAlloc.Telemetry.Generator/Models/MethodModel.cs#L20-L26

Records compare `IReadOnlyList<T>` and `ImmutableArray<T>` by reference, and a `Diagnostic`
carries a `Location` tied to one compilation. So every edit produces a model unequal to the last,
and `RegisterSourceOutput` re-runs for every `[Instrument]` interface on every keystroke.
1.7.0 adds `MetricModel` and `ResultMetrics`, which have the same problem.

**Why it matters**
IDE responsiveness in solutions with many instrumented interfaces. It is the same class of
problem as ZeroAlloc.Rest#319.

**Fix**
Replace the lists with an `EquatableArray<T>`, and carry diagnostics as an equatable record of
descriptor id, a location as file path plus span, and message arguments, rebuilt into
`Diagnostic` in the output step. Add a test that runs the driver twice over an unchanged
compilation and asserts `IncrementalStepRunReason.Cached` for the output step.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
```

```bash
gh issue create --repo ZeroAlloc-Net/ZeroAlloc.Telemetry \
  --title "Tag attributes fail silently: unresolved [TraceTag] paths and orphaned tag attributes" --body-file - <<'EOF'
Both need a severity decision, because reporting them adds a diagnostic to code that builds today.

**1. An unresolved `[TraceTag(name, member)]` path records the whole argument**
When the member path does not resolve, the tag records the argument itself instead of failing:
`BuildParameters` leaves the access suffix null, and `WriteSpanStartAndTags` then emits the
argument. At 1.6.4:
- https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/87290e066c299a41fbaf3aea894d456fcbbdc494/src/ZeroAlloc.Telemetry.Generator/InstrumentGenerator.cs#L517-L526
- https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/87290e066c299a41fbaf3aea894d456fcbbdc494/src/ZeroAlloc.Telemetry.Generator/ProxyWriter.cs#L178-L180

So `[TraceTag("batch.size", "Cuont")] IReadOnlyList<Chunk> chunks` compiles and tags the list
object. 1.7.0 added ZTEL007 for result paths, where a bad path already failed to compile. Here
it compiles, so an Error would break builds. Fix: report ZTEL007 from the same
`PathResolver.Resolve` result, and decide whether it is an Error, which would be a major, or a
Warning.

**2. ZTEL003 does not cover `[TraceTagFromResult]` or `[TraceTagConstant]`**
`Initialize` registers ZTEL003 for `[Trace]`, `[Count]`, `[Histogram]`, `[CountFromResult]` and
`[HistogramFromResult]` only. At 1.6.4:
https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/87290e066c299a41fbaf3aea894d456fcbbdc494/src/ZeroAlloc.Telemetry.Generator/InstrumentGenerator.cs#L83-L85

A method-level tag attribute on a type without `[Instrument]` is ignored without a word. Fix:
two more `RegisterMethodAttributeDiagnostic` calls. It is a new warning on existing code, so it
breaks TreatWarningsAsErrors consumers who have one.

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
```

- [ ] **Step 8: Push and open the PR**

Run `git log --format='%B' origin/main..HEAD | grep -n "claude.ai/code/session_"` and expect no output. If a harness trailer slipped in, amend that commit's message to remove it before pushing.

Run `git log --format='%B' origin/main..HEAD | awk 'length > 100'` and expect no output.

```bash
git push -u origin feat/metrics-from-result
gh pr create --repo ZeroAlloc-Net/ZeroAlloc.Telemetry --base main --head feat/metrics-from-result \
  --title "feat(generator): record metrics from the return value" --body-file - <<'EOF'
Closes #142.

Adds `[CountFromResult]` and `[HistogramFromResult]`, and `When`, `Unit` and `Description` on
`[Count]` and `[Histogram]`. Member paths and `When` guards are now validated in the generator,
as ZTEL007 to ZTEL009. Fixes the bugs this code touched on the way, listed below.

Spec: `docs/plans/2026-09-26-metrics-from-result-design.md`
Plan: `docs/plans/2026-09-26-metrics-from-result.md`

BEGIN_COMMIT_OVERRIDE
feat(generator): record metrics from the return value, and guard Count and Histogram with When
feat(generator): report unresolved member paths and non-bool When guards as ZTEL007 and ZTEL008
fix(generator): key metric fields by instrument kind and emit valid, distinct identifiers
fix(generator): detect Task and ValueTask returns by symbol instead of by name
fix(generator): declare the catch variable only when the span reads it
fix(generator): keep null-safe access after a Value segment on a nullable value type
fix(core): use constructor arguments in the InstrumentAttribute example
END_COMMIT_OVERRIDE

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
```

- [ ] **Step 9: After merge, verify the release**

Once the PR is squash-merged, open the release-please PR. Check that it proposes 1.7.0 and that every override entry above appears in its changelog: a green workflow does not prove a commit was counted. Check that it carries the `chore: mark analyzer rules and public api shipped in 1.7.0` commit. After the release PR merges, confirm on nuget.org that `ZeroAlloc.Telemetry` 1.7.0 and `ZeroAlloc.Telemetry.Generator` 1.7.0 are published.

---

## Design decisions this plan resolves

1. **Field key and shared metadata.** Fields are keyed by `(MetricKind, metric)`, as the spec says, so a `[Count("x")]` and a `[CountFromResult("x", ...)]` share one `Counter<long>`. When several attributes share a field, the first non-null `Unit` and the first non-null `Description`, in declaration order, are used. The spec does not cover conflicting metadata on one name. This rule is deterministic and lets a user declare each value once, and the docs say so.
2. **Identifier algorithm.** `_` plus the name, with anything that is not a letter, digit or `_` replaced by `_`. A name that would equal a proxy member or local (`_meter`, `_result`, `_tagged`, `_sw`…) or match a local pattern (`_tag_*`, `_spanName_*`, `_readN`) gets the prefix `_metric`. Collisions then get `_2`, `_3` in first-use order. Existing names such as `_orders_created` are unchanged, so no existing snapshot moves.
3. **Literal escaping widened.** Metric, activity-source, span and tag names now all go through `SymbolDisplay.FormatLiteral`. Only metric names were in scope, but the same pre-existing bug sits in the same writer methods, so it is fixed in the Task 1 commit.
4. **Async detection.** Task shapes are matched on the containing namespace plus the metadata name (`Task`, ``Task`1``, `ValueTask`, ``ValueTask`1``), not with `Compilation.GetTypeByMetadataName`. That returns null when two referenced assemblies define the type, as with a `ValueTask` polyfill, which would silently make everything synchronous.
5. **Extra fix: CS0168 in generated code.** A proxy method with `[Count]` or `[Histogram]` but no `[Trace]` emitted `catch (Exception _ex)` and never read `_ex`. I checked this: the compiler reports CS0168 in the generated file, which breaks every TreatWarningsAsErrors consumer, and the new runtime test project would have hit it. It is fixed as its own `fix:` commit, and the compile test now fails on generated-code warnings and generator diagnostics.
6. **Extra fix: nullable `Value` segment.** `Value.Width` on `Task<Extent?>` emitted `_tagged.Width`, and `When = "Value"` on `Task<bool?>` emitted `if (_tagged)`. Both fail to compile. This is in the shared resolver the feature reuses, so it gets its own `fix:` commit in Task 3.
7. **ZTEL007 scope.** It is reported for `[TraceTagFromResult]`, `[CountFromResult]`, `[HistogramFromResult]` and every `When`, as the spec lists, but not for `[TraceTag]` parameter paths. An unresolved parameter path compiles today and silently tags the whole argument, so an Error there would break building code, against the spec's "no existing code that compiled stops compiling". That goes into follow-up issue 3 for a severity decision.
8. **Invalid attributes are dropped from emission.** After ZTEL007, ZTEL008 or ZTEL009, the tag or instrument is not emitted, so the diagnostic is the only error the user sees. That covers a `When` error on `[Count]`/`[Histogram]` too. The old "emit as written, let the compiler complain" fallbacks are removed.
9. **ZTEL005 details.**
   - It keeps its method-identifier location, which is where it has always been reported. The spec's "argument location" rule is stated for the new IDs.
   - `{0}` is `TraceTagFromResult`, `CountFromResult` or `HistogramFromResult`, and for a guard it is written as it appears, for example `Count(When = "IsSuccess")`.
   - A guarded `[Count]`/`[Histogram]` on a method with no result records nothing. Recording unguarded would count exactly the calls the guard excludes. This matches the message, "records nothing".
   - Path resolution is skipped on such methods, so no spurious ZTEL007 is reported against `Task`.
10. **Type sets.** These are explicit `SpecialType` sets rather than `ClassifyConversion`, which would also admit `char` and user-defined implicit conversions.
    - Counter: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`.
    - Histogram: those plus `ulong`, `float`, `double`, `decimal`.
    - Excluded from both: `char`, `nint`/`nuint` and enums.
    - Nullable forms are checked through `UnwrapNullable`, and a trailing dropped `Value` resolves to the underlying type with `CanBeNull` set.
11. **Empty member.** It reads the awaited return value itself, as `[TraceTagFromResult]` does, so `[CountFromResult("batch.items", "")]` works on `Task<int>`.
12. **Release tracking.** ZTEL005's title and message change, but its category and severity do not. Release tracking records only category and severity, so no `Changed Rules` row is added, which answers the spec's "if the release file tracks it". The ZTEL003 title is reworded the same way.
13. **Emission shape.**
    - Result tags come first, then result-driven instruments in attribute order, then `[Count]`, then `[Histogram]`. All sit inside the `try`, after the inner call.
    - `var _tagged = _result;` is emitted once, whenever the result can be null and anything reads it.
    - A nullable value is bound with `is { } _readN`, which skips null and yields a non-nullable local. The guard is joined with `&&`, so the member is never read when the guard is false.
    - `decimal` gets `(double)`.
    - A method with only result-driven instruments still gets the `try`/`catch (Exception) { throw; }` wrapper, the same shape as today's `[Count]`-only method.
14. **Attribute syntax.** Explicit constructors are used rather than the primary constructors shown in the spec, matching every other attribute in the package. The public surface, and so every PublicAPI line, is identical.
15. **Runtime test project.** It is a new project, `tests/ZeroAlloc.Telemetry.Generated.Tests`, because the existing runtime project declares nested `[Instrument]` interfaces with hand-written proxies that the generator would also try to proxy. CI runs `dotnet test ZeroAlloc.Telemetry.slnx`, so adding it to the solution is the CI wiring, and `ci.yml` is unchanged.
16. **`docs/source-generator.md` had no diagnostic table.** The plan adds one covering ZTEL001–ZTEL009, rather than only the new rows the spec mentions.
17. **Cleanups in touched files.**
    - The sample's redundant `<NoWarn>EPC12</NoWarn>` is removed, because the generated file already carries the pragma.
    - The orphaned Verify-era `.verified.txt` snapshot is deleted.
    - The benchmarks project's `NoWarn` is outside this change and is left alone.
18. **Known, accepted edges of "nothing that compiled stops compiling".**
    - A `When` bound to a type with an implicit conversion to `bool` compiled before and is now ZTEL008. The spec defines ZTEL008 as "not bool or bool?".
    - A result path through a C# 14 extension property in scope of the generated file compiled before and is now ZTEL007, because the resolver sees only real members.
    - Both are rare, and both follow the spec's own definitions.
19. **PR override block.** It lists the spec's three entries plus the validation `feat:` and three further `fix:` entries: the catch variable, the nullable `Value` segment and the doc example. Each is a user-visible change that would otherwise be lost in the squash.
20. **Follow-up issues.** The two issues the spec names are filed, plus a third grouping two verified silent-failure gaps that need a severity decision (decision 7 and the ZTEL003 coverage). All three are checked against existing issues first.

---

## Spec coverage

| Spec requirement | Task |
|---|---|
| `[CountFromResult]`: `Counter<long>`, `.Add(member)`, implicit-to-long types incl. nullable, null not recorded, negative recorded as is | 4 (API), 5 (emission, `Fits`), 6 (runtime) |
| `[HistogramFromResult]`: `Histogram<double>`, `.Record`, numeric types incl. nullable, `(double)` for decimal, null not recorded | 4, 5, 6 |
| `When` has `[TraceTagFromResult]` semantics, and the member is not read when false | 3 (`TryBuildGuard`), 5 (`&&` emission), 6 (throwing `Value` in `ChatResult`) |
| `When` on `[Count]`: `Add(1)` only on non-throwing return with guard true | 5, 6 (`FailedResult_RecordsNothing_AndIsNotCounted`) |
| `When` on `[Histogram]`: success-only, nothing on throw; unguarded unchanged | 5 (`WriteCatchBlock`, `GeneratesGuardedCountAndHistogram`), 6 (`Throw_RecordsNothing_ForGuardedInstruments`) |
| `Unit`/`Description` pass through on all four attributes | 4 (Count/Histogram), 5 (result-driven), 6 (`UnitAndDescription_ReachThePublishedInstrument`) |
| Reads sit with result tags, in the same `try`, reusing `_tagged` | 5 (`WriteResultReads`, Chat snapshot) |
| Two instruments on one method each get their own field | 5 (Chat snapshot), 1 (`MetricFieldTable`) |
| void/Task/ValueTask: new attributes and `When` report ZTEL005, reworded with `{0}` | 3 (reword), 5 (new cases) |
| No `[Trace]` needed | 5 (Plain snapshot and runtime interface have none) |
| ZTEL007 at the argument, naming the segment and type, for all result attributes and `When` | 3, 5 |
| ZTEL008 for a non-bool `When` | 3, 5 |
| ZTEL009 for both instruments | 5 |
| ZTEL003 for the two new attributes | 4 |
| Public API additive, all in `PublicAPI.Unshipped.txt` | 4 |
| Fix: metric fields keyed by kind plus name, sanitizer and dedup | 1 |
| Fix: async detection by symbol | 2 |
| Fix: `InstrumentAttribute` XML example | 7 |
| Snapshots: both new attributes with and without When/Unit/Description; nullable and value-type paths; decimal; two instruments; When on Count/Histogram with no throw-path record; `Result<T, E>` and plain `T` | 5 (`ResultMetricTests`), 4 (unit snapshot), 1 (fields) |
| Diagnostic tests assert ID, severity, location and message | 3, 4, 5 (`DiagnosticTests`) |
| Compile tests with nullable enabled, including the fixed field-name cases | 1 (`FieldNameProbe_Compiles`), 2, 3, 5 (`ResultMetricsProbe_Compiles`) |
| Generator-backed runtime test with `MeterListener`: values, false guard, null member, failed Result not counted, unit/description | 6 |
| AOT smoke gains a result-driven counter | 6 |
| Docs: attributes.md, README features line, source-generator.md diagnostics rows and reworded ZTEL005 | 7 |
| `AnalyzerReleases.Unshipped.md`: ZTEL007–ZTEL009; ZTEL005 change only if tracked | 3, 5, Design decision 12 |
| Follow-ups filed: `[MetricTagFromResult]`, non-equatable models | 7 Step 7 |
| One PR closes #142, 1.7.0, `BEGIN_COMMIT_OVERRIDE` with feat and both fixes | 7 Step 8, 9 |
| Commit bodies ≤100-char lines, no nested parentheses | every commit step; 7 Step 8 check |
