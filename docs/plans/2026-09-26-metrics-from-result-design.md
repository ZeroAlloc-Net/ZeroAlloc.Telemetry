# Metrics from the result: design

**Issue:** #142. **Release:** ZeroAlloc.Telemetry 1.7.0, a minor. **Status:** approved 2026-09-26. The user approved the outline in chat, then this spec was written unattended from it.

This is sub-project C of the Result-integration cluster:
- **A**, ZeroAlloc.Rest 2.1.0: error body and error mappers.
- **B**, ZeroAlloc.Resilience 3.2.0: retrying failed Results.

## Problem

- **Counts and histograms can't read the return value.** `[Count]` can only add 1, and `[Histogram]` can only record elapsed milliseconds. Neither can record a value carried by the return value, such as tokens consumed or a confidence score.
- **`[Count]` counts failed Results as successes.** It fires on any non-throwing return, including a method that returns a failed `Result<T, E>`.
- **Spans aren't a substitute.** `[TraceTagFromResult]` can already put such values on the span, with a `When` guard, but spans are sampled and can't be aggregated the way a counter can.

## Current state, 1.6.4

- `[Count(metric)]` and `[Histogram(metric)]` take only a name. Nothing passes a unit or a description.
- The proxy emits `private static readonly Counter<long>` and `Histogram<double>` fields, created from one static `Meter`. Inside the method's `try`, after the inner call and after the result tags, it emits `.Add(1)`. The histogram records elapsed milliseconds on success and in the `catch`.
- `[TraceTagFromResult(name, member) { When = "IsSuccess" }]` resolves `member` and `When` as dotted paths against the awaited return value. That value is the `Result` itself, so paths are written `Value.X`. The guard is emitted as an `if` around the tag.
  - `?.` is inserted for nullable steps.
  - Nothing is validated: a bad path or a non-bool `When` only fails as a compile error in generated code.
- **Diagnostics:** ZTEL001 to ZTEL006 exist. Analyzer release tracking and PublicAPI tracking are active.
- **Runtime tests** use hand-written proxies. The generator is covered by snapshot, diagnostic and compile tests.

## Decisions

- **Reuse `[TraceTagFromResult]`'s path and guard machinery** for the new attributes. That gives one resolver, one set of nullable rules and one meaning of `When`.
- **Validate paths and guards in the generator.** The new diagnostics also apply to `[TraceTagFromResult]`: a mistake that used to surface as a compile error in generated code now gets a clear diagnostic at the attribute.
- **Defer `[MetricTagFromResult]`,** which adds a dimension, to a follow-up issue.
- **Fix the existing bugs this change touches,** each as its own `fix:`:
  - metric field name collisions;
  - async detection by substring;
  - the `InstrumentAttribute` doc example that doesn't compile.

## Public API (additive)

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class CountFromResultAttribute(string metric, string member) : Attribute
{
    public string Metric { get; } = metric;
    public string Member { get; } = member;     // dotted path from the awaited return value
    public string? When { get; set; }           // dotted path to a bool member; null = always
    public string? Unit { get; set; }
    public string? Description { get; set; }
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class HistogramFromResultAttribute(string metric, string member) : Attribute
{
    // same members as CountFromResultAttribute
}
```

`CountAttribute` and `HistogramAttribute` gain `When`, `Unit` and `Description`, all `{ get; set; }` and defaulting to null. Every new member goes into `PublicAPI.Unshipped.txt`.

## Behaviour

- **`[CountFromResult]`** creates `Counter<long>`, then `.Add(<member value>)`.
  - The member type must convert to `long` implicitly: `int`, `long`, `short`, `byte`, `uint`, `ushort`, `sbyte`, or their nullable forms.
  - A null value is not recorded. A negative value is recorded as it is; counters are expected to be monotonic, and the docs say so.
- **`[HistogramFromResult]`** creates `Histogram<double>`, then `.Record(<member value>)`.
  - The member type must be numeric and convert to `double`: every integral type, `float`, `double`, `decimal`, or their nullable forms.
  - `decimal` is converted with an explicit `(double)` cast. A null value is not recorded.
- **`When`** has exactly the semantics of `[TraceTagFromResult]`: a dotted path from the awaited return value to a `bool` or `bool?` member. The instrument is recorded only when it is `true`, and the member is not read otherwise.
- **`When` on `[Count]`:** `Add(1)` happens only on a non-throwing return where the guard is true.
- **`When` on `[Histogram]`:**
  - On a non-throwing return, elapsed milliseconds are recorded only when the guard is true.
  - When the call throws, there is no result to evaluate, so a guarded histogram records nothing.
  - An unguarded histogram still records on both paths, as today.
- **`Unit` and `Description`** pass through to `CreateCounter` and `CreateHistogram` on all four attributes.
- **Where the reads happen.** Result-driven instruments sit with the result tags, after the inner call and inside the same `try`. The null-state copy (`_tagged`) is reused. Instruments with the same metric name on one method, such as two `[CountFromResult]`s for input and output tokens with different names, each get their own field.
- **Method shapes.** `void`, `Task` and `ValueTask` methods have no result. The new attributes and `When` on them report ZTEL005, which is reworded so it names the attribute.
- **No `[Trace]` needed.** Unlike result tags, the new attributes don't require `[Trace]`: they are metrics, not span data.

## Diagnostics

The new IDs continue from ZTEL006. All are reported at the attribute's argument location.

- **ZTEL007, Error: member path not found.** A segment of `Member` or `When` names no property or field on the type reached so far. The message names the segment and the type. It applies to `[TraceTagFromResult]`, `[CountFromResult]`, `[HistogramFromResult]` and `When` on all of them.
- **ZTEL008, Error: `When` is not a boolean.** The `When` path resolves to a member that isn't `bool` or `bool?`.
- **ZTEL009, Error: the member doesn't fit the instrument.** `[CountFromResult]`'s member doesn't convert to `long`, or `[HistogramFromResult]`'s member isn't numeric.
- **ZTEL005, reworded:** it takes `{0}`, the attribute name, and is also reported for the new attributes and for `When` on `[Count]` and `[Histogram]` when the method returns no value.
- **ZTEL003:** it gains registrations for the two new attributes on a method outside an `[Instrument]` interface.

A path error used to be a compile error in generated code, so ZTEL007 and ZTEL008 turn an existing hard error into a clearer one. No existing code that compiled stops compiling.

## Fixes in touched code

- **Metric fields.**
  - Counter and histogram names share one `HashSet`, so `[Count("x")]` and `[Histogram("x")]` on one interface leave the histogram field unemitted, and generated code fails to compile.
  - Names collapse: `a.b` and `a_b` both become `_a_b`.
  - Characters other than `.` and `-` produce invalid identifiers.

  Fix: key fields by instrument kind plus metric name, and generate valid, distinct identifiers with a sanitizer and a counter-based deduplication.
- **Async detection.** `returnType.IndexOf("Task") >= 0` misreads types such as `MyTaskResult`. Detect `System.Threading.Tasks.Task`, `Task<T>`, `ValueTask` and `ValueTask<T>` by symbol.
- **`InstrumentAttribute` XML example.** It uses `[Count(Metric = ...)]` and `[Trace(Name = ...)]`, which don't compile because those properties are get-only. Use constructor arguments.

## Tests

- **Generator snapshots:**
  - `[CountFromResult]` and `[HistogramFromResult]`, with and without `When`, `Unit` and `Description`;
  - nullable and value-type paths;
  - `decimal`;
  - two instruments from one method;
  - `When` on `[Count]` and on `[Histogram]`, the guarded histogram having no record on the throw path;
  - `Result<T, E>` and plain `T` returns.
- **Diagnostic tests:** ZTEL007 for a missing segment, on each attribute kind; ZTEL008; ZTEL009 for both instruments; the reworded ZTEL005; ZTEL003 for the new attributes. Each asserts the ID, severity, location and message.
- **Compile tests:** every new shape compiles with nullable enabled, and so do the fixed field-name cases.
- **Runtime tests.** The existing runtime tests use hand-written proxies, so a generator-backed test is needed: a test project that references the generator as an analyzer and declares its own `[Instrument]` interfaces. Using `MeterListener`, it asserts:
  - the recorded values;
  - no value is recorded when `When` is false or the member is null;
  - `[Count(When = "IsSuccess")]` doesn't count a failed Result;
  - unit and description reach the published instrument.
- **AOT smoke:** the sample gains one result-driven counter.

## Docs

- `docs/attributes.md`: the two new attributes, the `When`/`Unit`/`Description` properties, the paths, the type rules, the counter-monotonicity note, and the guarded-histogram-on-throw rule.
- `README.md`: a features line.
- `docs/source-generator.md`: the diagnostic table rows for ZTEL007 to ZTEL009, and the reworded ZTEL005.
- **Rules:** `AnalyzerReleases.Unshipped.md` gets ZTEL007 to ZTEL009, plus the ZTEL005 message change if the release file tracks it.

## Follow-ups (issues, not this change)

- `[MetricTagFromResult]`, dimensions read from the result.
- Generator models aren't value-equatable (`IReadOnlyList` and `ImmutableArray<Diagnostic>` inside records), so incremental caching never hits. This is the same class of problem as ZeroAlloc.Rest#319.

## Delivery

- One PR closes #142, released as 1.7.0.
- The PR body carries a `BEGIN_COMMIT_OVERRIDE` block listing:
  - `feat:` metrics from the result and `When` on `[Count]`/`[Histogram]`;
  - `fix:` metric field name collisions;
  - `fix:` async detection by symbol.
- Commit bodies have lines of at most 100 characters and no nested parentheses.
