---
id: attributes
title: Attribute Reference
slug: /docs/attributes
description: Reference for [Instrument], [Trace], [Count], [Histogram], [CountFromResult], [HistogramFromResult], [TraceTag], [TraceTagFromResult], and [TraceTagConstant] — the attributes in ZeroAlloc.Telemetry.
sidebar_position: 3
---

# Attribute Reference

## [Instrument]

```csharp
[AttributeUsage(AttributeTargets.Interface)]
public sealed class InstrumentAttribute : Attribute
{
    public string ActivitySource { get; }
    public bool PublicProxy { get; set; }
    public string? Version { get; set; }
    public InstrumentAttribute(string activitySource);
}
```

**Placement:** Interface only.

**Effect:** Triggers the source generator. The generator emits a sealed proxy class named `{TypeName}Instrumented` (leading `I` stripped) next to the interface: in the same namespace, or for a nested interface in the same containing type, which must then be `partial`. A generic interface gets a generic proxy with the same type parameters and constraints. A generic method gets a generic proxy method with its constraints. See [Nested and generic interfaces](source-generator.md#nested-and-generic-interfaces).

**`activitySource`:** The name used for both the static `ActivitySource` and the static `Meter` field in the generated proxy. Typically a dotted component name: `"MyApp.Orders"`, `"ZeroAlloc.EventSourcing"`.

```csharp
[Instrument("MyApp.Payments")]
public interface IPaymentGateway { ... }
// Emits: PaymentGatewayInstrumented : IPaymentGateway
//   ActivitySource name: "MyApp.Payments"
//   Meter name:          "MyApp.Payments"
```

**`PublicProxy`:** Emits the proxy as `public` instead of the default `internal`.

An internal proxy can only be constructed from the assembly that declares the interface. That is fine for the common case, but not when the interface lives in a shared abstractions assembly and is implemented across several packages — those packages cannot wrap their own implementations, and the abstractions assembly is exactly where an extra dependency is least welcome.

```csharp
[Instrument("MyApp.Shared", PublicProxy = true)]
public interface ISharedService { ... }
// Emits: public sealed class SharedServiceInstrumented : ISharedService
```

It is opt-in because it widens the declaring assembly's public API surface.

**`Version`:** The version given to the `ActivitySource` and the `Meter`, so a backend can tell
instrumentation versions apart. By default it is the declaring assembly's informational version,
which the .NET SDK sets from the project's `Version`, read when the proxy is generated:

```csharp
[Instrument("MyApp.Payments")]                     // new("MyApp.Payments", "1.4.0+5f2c1e9")
[Instrument("MyApp.Payments", Version = "2.0.0")]  // new("MyApp.Payments", "2.0.0")
[Instrument("MyApp.Payments", Version = "")]       // new("MyApp.Payments"), no version
```

An assembly without an informational version gets no version, as before.

---

## [Trace]

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class TraceAttribute : Attribute
{
    public string Name { get; }
    public ActivityKind Kind { get; set; }
    public string? ErrorWhen { get; set; }
    public string? ErrorDescription { get; set; }
    public bool TagsAtStart { get; set; }
    public TraceAttribute(string name);
}
```

**Placement:** Interface method.

**Effect:** Wraps the method body in an `Activity` span.

- Span is started with `ActivitySource.StartActivity("name")` before the call.
- Span is stopped automatically via `using` (disposed in `finally`).
- On exception: `activity?.SetStatus(ActivityStatusCode.Error, ex.Message)` then rethrow.

```csharp
[Trace("payment.charge")]
ValueTask<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct);
```

Generated:
```csharp
using var _activity = _activitySource.StartActivity("payment.charge");
try { ... }
catch (Exception _ex)
{
    _activity?.SetStatus(ActivityStatusCode.Error, _ex.Message);
    throw;
}
```

### Varying the span name by implementation

`[Trace]` goes on the interface method, so by default **every implementation
produces the same span name**. On an interface with one implementation that is
fine. On one with several it is a problem: a slow call shows a single span name
and gives no way to tell which implementation was the cost.

The `{type}` token substitutes the wrapped implementation's type name:

```csharp
[Instrument("ragnet")]
public interface IVectorStore
{
    [Trace("vectorstore.search.{type}")]
    Task<IReadOnlyList<SearchResult>> SearchAsync(string collection, CancellationToken ct);
}
```

A proxy wrapping `QdrantVectorStore` emits spans named
`vectorstore.search.QdrantVectorStore`; one wrapping `WeaviateVectorStore` emits
`vectorstore.search.WeaviateVectorStore`. The two are now distinguishable in any
trace UI, and group separately.

This also replaces the common workaround of tagging the type by hand:

```csharp
// No longer needed — the distinction lives in the span name, where a trace UI groups on it.
activity?.SetTag("vector.store", GetType().Name);
```

The token may appear anywhere in the name, and more than once. `{type}` alone is
a valid name, yielding just the type name.

**Cost: nothing per call.** The wrapped instance cannot change for the lifetime
of a proxy, so the name is composed once in the constructor and reused:

```csharp
private readonly string _spanName_SearchAsync_0;

public VectorStoreInstrumented(IVectorStore inner)
{
    _inner = inner;
    var _implName = inner.GetType().Name;
    _spanName_SearchAsync_0 = "vectorstore.search." + _implName;
}

private async Task<...> _core_SearchAsync_0(...)
{
    using var _activity = _activitySource.StartActivity(_spanName_SearchAsync_0);
```

A name with no token is still emitted as a plain string literal, so nothing
changes for existing code.

### Naming the span from its parameters

`{parameter}` and `{parameter.Member}` tokens take the value of an argument,
which is how the OpenTelemetry GenAI conventions name a span:
`{gen_ai.operation.name} {gen_ai.request.model}`.

```csharp
[Trace("{operation} {request.Model}")]
ValueTask<ChatResponse> ChatAsync(string operation, ChatRequest request, CancellationToken ct);
```

A sampler decides before the name could be composed, and an unsampled call
should build no string, so the span starts under the constant part of the name
and gets the full name as its `DisplayName` only when it was sampled:

```csharp
using var _activity = _activitySource.StartActivity("ChatAsync");
if (_activity is not null)
{
    var _nameArg0 = request;
    _activity.DisplayName = string.Create(CultureInfo.InvariantCulture, $"{operation}{" "}{_nameArg0?.Model}");
}
```

The constant part is the name with the parameter tokens left out and its
whitespace collapsed, such as `chat` for `"chat {model}"`. When nothing is left,
as above, it is the method name. Values are formatted with the invariant
culture, and a null is empty. A member path that does not resolve is reported as
**ZTEL022** and the token is left out. `{type}` can be combined with parameter
tokens, and keeps its meaning even when a parameter is named `type`.

Any other token in braces is emitted verbatim and reported as **ZTEL006**,
rather than leaving a literal brace in a span name to be discovered on a
dashboard later.

### Span kind

`Kind` sets the span's `ActivityKind`, such as `Client` for a call to a remote
service: `StartActivity("chat", ActivityKind.Client)`. The default, `Internal`,
emits the call it always did.

### Error status from the result

A method that reports failure in its result, such as a `Result<T, E>`, never
throws, so its span was recorded as a success. `ErrorWhen` names a `bool` or
`bool?` member of the awaited result that marks the span as an error, and
`ErrorDescription` an optional member for the status description. A member that
is not a string is converted with `ToString()`.

```csharp
[Trace("chat", ErrorWhen = "IsFailure", ErrorDescription = "Error.Message")]
ValueTask<Result<ChatResponse, ChatError>> ChatAsync(ChatRequest request, CancellationToken ct);
```

```csharp
// Generated, after the result tags:
if (_activity is not null && _result.IsFailure)
    _activity.SetStatus(ActivityStatusCode.Error, _result.Error?.Message);
```

The paths resolve like `When`: a path that names nothing is **ZTEL007**, an
`ErrorWhen` that is not a bool is **ZTEL008**, and `ErrorWhen` on a method with
no result is **ZTEL005**. `ErrorDescription` is only read on an error, and has
no effect without `ErrorWhen`. An exception still sets the error status as
before.

### Tags a sampler can see

By default the `[TraceTagConstant]` and `[TraceTag]` tags are set right after
the span starts, so a sampler deciding at the start does not see them.
`TagsAtStart = true` passes them to `StartActivity` instead, as the GenAI
conventions recommend:

```csharp
using var _activity = _activitySource.HasListeners()
    ? _activitySource.StartActivity("chat", ActivityKind.Client, default(ActivityContext), _startTags_ChatAsync_0(operation, request, ct))
    : null;
```

The tags are collected by a generated method, and only when the source has a
listener. `StartActivity` takes them as an enumerable, so they are boxed into one
`TagList` per call while something listens, sampled or not. With no listener
nothing is allocated. A null value adds no tag.

---

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

Attributes that share a metric name and kind share one instrument. The first `Unit` and the first `Description` set on any of them are the ones used, taking them in the order the generator visits them: interface members top to bottom, and within a method `[Count]`, then `[Histogram]`, then `[CountFromResult]` and `[HistogramFromResult]` in attribute order. Declare each once, so the order does not matter.

---

## [Histogram]

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class HistogramAttribute : Attribute
{
    public string Metric { get; }
    public string? When { get; set; }
    public string? Unit { get; set; }
    public string? Description { get; set; }
    public double[]? Buckets { get; set; }
    public HistogramAttribute(string metric);
}
```

**Placement:** Interface method.

**Effect:** Records the elapsed time in a `Histogram<double>` on every call — including when the method throws.

Uses `Stopwatch.GetTimestamp()` before the call and `Stopwatch.GetElapsedTime(ts)` after, so the measurement includes the full method duration regardless of outcome.

`Unit` decides what is recorded, so the values match the unit the instrument declares:

| `Unit` | Recorded |
|---|---|
| none, `ms` | `TotalMilliseconds` |
| `s` | `TotalSeconds`, as the OpenTelemetry semantic conventions use |
| `us` | `TotalMicroseconds` |
| `ns` | `TotalNanoseconds` |
| `min` | `TotalMinutes` |
| `h` | `TotalHours` |

Any other unit is reported as **ZTEL018**, and the duration is recorded in milliseconds. Before 1.9 every duration was recorded in milliseconds whatever the unit, so a histogram declared with `Unit = "s"` reported values a thousand times too large.

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
    var _result = await _inner.ChargeAsync(request, ct).ConfigureAwait(false);
    _payment_charge_ms.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);
    return _result;
}
catch (Exception)
{
    _payment_charge_ms.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);
    throw;
}
```

### Bucket boundaries

`Buckets` passes explicit bucket boundaries to the instrument as `InstrumentAdvice<double>.HistogramBucketBoundaries`, which exporters such as OpenTelemetry's use instead of their defaults:

```csharp
[Histogram("gen_ai.client.operation.duration", Unit = "s",
    Buckets = new[] { 0.01, 0.02, 0.04, 0.08, 0.16, 0.32, 0.64, 1.28, 2.56, 5.12, 10.24, 20.48, 40.96, 81.92 })]
ValueTask<ChatResponse> CompleteAsync(ChatRequest request, CancellationToken ct);
```

```csharp
// Generated:
private static readonly Histogram<double> _gen_ai_client_operation_duration =
    _meter.CreateHistogram<double>("gen_ai.client.operation.duration", unit: "s", description: null, tags: null,
        advice: new InstrumentAdvice<double> { HistogramBucketBoundaries = new double[] { 0.01, 0.02, /* ... */ 81.92 } });
```

The boundaries must be finite and strictly increasing, or the generator reports **ZTEL019**: the runtime would otherwise throw when the proxy's static fields are initialised. Advice needs System.Diagnostics.DiagnosticSource 9.0, which .NET 9 and later include; on .NET 8 reference the 9.0 package, or the generator reports **ZTEL020**. When several methods record one metric, the first that declares buckets sets them. `[HistogramFromResult]` takes `Buckets` too.

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
    var _result = await _inner.AcceptAsync(orderId, ct).ConfigureAwait(false);
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
var _result = await _inner.CompleteAsync(request, ct).ConfigureAwait(false);
if (_result.IsSuccess && _result.Value?.Usage?.InputTokens is { } _read0)
    _llm_tokens_input.Add(_read0);
if (_result.IsSuccess && _result.Value?.Usage?.OutputTokens is { } _read1)
    _llm_tokens_output.Add(_read1);
```

- **Member type.** It must convert implicitly to `long`: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint` or `long`, or a nullable form of one. Anything else, including `ulong`, `char`, enums, floating-point types and `dynamic`, is **ZTEL009**.
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
    public double[]? Buckets { get; set; }
    public bool Each { get; set; }
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

- **Member type.** It must be numeric: `sbyte`, `byte`, `short`, `ushort`, `int`, `uint`, `long`, `ulong`, `float`, `double` or `decimal`, or a nullable form of one. Anything else, including `dynamic`, is **ZTEL009**.
- **`decimal`** has no implicit conversion to `double`, so it is converted with an explicit `(double)` cast.
- **Null** values are not recorded, and **a call that throws** records nothing, since there is no result.

---

### One measurement per element

With `Each = true`, every element of the member is recorded, such as the token count of each modality or the score of each retrieved document. The member must be a `ReadOnlySpan<double>`, `Span<double>`, `ReadOnlyMemory<double>`, `Memory<double>`, an array, or a type whose `GetEnumerator()` returns a struct, such as `List<double>` or `ImmutableArray<double>`, with numeric elements. Anything else, such as an `IEnumerable<double>`, whose enumerator would be allocated, is reported as **ZTEL009**. A span cannot be reached through a member that can be null; expose a `ReadOnlyMemory<double>` there instead.

```csharp
[HistogramFromResult("rag.document.score", "Scores", Each = true)]
ValueTask<Retrieval> RetrieveAsync(string query, CancellationToken ct);
```

```csharp
// Generated, Scores being a ReadOnlyMemory<double>:
if (_rag_document_score.Enabled)
    _recordEach(_rag_document_score, _result.Scores.Span);
```

The elements are only iterated when a listener has enabled the instrument, and iterating allocates nothing. A null element is skipped. Tags are built once and shared by every element's measurement.

## [MetricTagFromResult]

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MetricTagFromResultAttribute : Attribute
{
    public string Name { get; }
    public string Member { get; }
    public string? When { get; set; }
    public string? Metric { get; set; }
    public MetricTagFromResultAttribute(string name, string member);
}
```

**Placement:** Method. Does not need `[Trace]`. May be applied more than once; the tags combine.

**Effect:** Adds the value of `member` as a tag, a dimension, on the metrics the method records. For dimensions known only once the call returns, such as the versioned model id a service answered with.

```csharp
[Count("llm.requests", When = "IsSuccess")]
[CountFromResult("llm.tokens.input", "Value.Usage.InputTokens", When = "IsSuccess", Unit = "{token}")]
[HistogramFromResult("llm.cost", "Value.Cost", When = "IsSuccess", Unit = "USD")]
[MetricTagFromResult("gen_ai.response.model", "Value.Model", When = "IsSuccess")]
[MetricTagFromResult("llm.cache.hit", "Value.CacheHit", When = "IsSuccess", Metric = "llm.cost")]
ValueTask<Result<ChatResponse, ChatError>> CompleteAsync(ChatRequest request, CancellationToken ct);
```

```csharp
// Generated for llm.cost, for a Result that is a struct and a ChatResponse that is a class:
if (_llm_cost.Enabled && _result.IsSuccess && _result.Value?.Cost is { } _read1)
{
    var _metricTags1 = new TagList();
    if (_result.IsSuccess && _result.Value?.Model is { } _metricTag1_0)
        _metricTags1.Add("gen_ai.response.model", _metricTag1_0);
    if (_result.IsSuccess && _result.Value?.CacheHit is { } _metricTag1_1)
        _metricTags1.Add("llm.cache.hit", _metricTag1_1);
    _llm_cost.Record((double)_read1, in _metricTags1);
}
```

- **Which metrics.** Without `Metric`, the tag goes on every metric the method records: `[Count]`, `[Histogram]`, `[CountFromResult]` and `[HistogramFromResult]`. `Metric = "name"` restricts it to the instruments of that one metric name.
- **Only when someone is listening.** The tag list is built inside a check of the instrument's `Enabled`, which comes first. With no listener, the proxy neither reads the member nor boxes its value, so the call stays allocation-free. A disabled instrument drops every measurement anyway, so skipping the call loses nothing.
- **Tag values box** once per enabled instrument they are added to, since a tag value is an `object`. Strings do not box.
- **Null.** A null value, or a null anywhere along the path, adds no tag. The measurement is still recorded, as `Activity.SetTag` records a span without a null tag.
- **`When`.** When the guard is false, the member is not read and no tag is added; the measurement is still recorded if its own guard holds. Guard the tag as well as the metric when the member lives on one branch of a `Result`, as `Value.Model` does.
- **A call that throws** leaves no result to read. An unguarded `[Histogram]` still records on that path, without result tags.
- **Any member type** works. Exporters handle strings, booleans and numbers natively; prefer those over types that are formatted with `ToString()`.
- **An empty member** tags the return value itself.

Diagnostics:

- A `Metric` that names no metric the method records, or a method that records no metric at all, is **ZTEL011**, a warning. The tag would be added to nothing.
- The same tag name added to one metric by two `[MetricTagFromResult]` is **ZTEL012**, an error, reported on the later one. A measurement carries one value per tag name. The same name restricted to two different metrics is fine.
- A bad path or `When` is **ZTEL007** or **ZTEL008**, and a method with no return value is **ZTEL005**, as for the other result attributes.

---

## [MetricTag] and [MetricTagConstant]

```csharp
[AttributeUsage(AttributeTargets.Parameter, AllowMultiple = true)]
public sealed class MetricTagAttribute : Attribute
{
    public string Name { get; }
    public string? Member { get; }
    public string? Metric { get; set; }
    public MetricTagAttribute(string name);
    public MetricTagAttribute(string name, string member);
}

[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class MetricTagConstantAttribute : Attribute
{
    public string Name { get; }
    public object? Value { get; }
    public string? Metric { get; set; }
    public MetricTagConstantAttribute(string name, object? value);
}
```

**Placement:** `[MetricTag]` on a parameter, `[MetricTagConstant]` on an interface method.

**Effect:** The metric counterparts of `[TraceTag]` and `[TraceTagConstant]`: they add an argument, a member of one, or a constant as a tag on every metric the method records. `Metric` restricts a tag to the instruments of one metric name, as on `[MetricTagFromResult]`. These are the dimensions the OpenTelemetry GenAI conventions put on their metrics:

```csharp
[Histogram("gen_ai.client.operation.duration", Unit = "s")]
[HistogramFromResult("gen_ai.client.token.usage", "Usage.Tokens", Each = true, Unit = "{token}")]
[MetricTagConstant("gen_ai.operation.name", "chat")]
[MetricTagConstant("gen_ai.provider.name", "openai")]
[MetricTagConstant("gen_ai.token.modality", "text", Metric = "gen_ai.client.token.usage")]
ValueTask<ChatResponse> CompleteAsync(
    [MetricTag("gen_ai.request.model", "Model")] ChatRequest request,
    CancellationToken ct);
```

Like every metric tag, they are only built behind the instrument's `Enabled`, so a call with no listener neither reads the argument nor boxes it. Constants come first, then parameter tags in parameter order, then `[MetricTagFromResult]` tags. Unlike result tags, they are known before the call, so the measurement an unguarded `[Histogram]` records when the call throws carries them too.

A null value, or a null anywhere along the path, adds no tag, and so does a null constant. A path that names no member is reported as **ZTEL010**, a constant that is an array or a type as **ZTEL021**, a tag on no metric as **ZTEL011**, and two tags with one name on one metric, whatever their kinds, as **ZTEL012**.

## Member paths and When

`[TraceTagFromResult]`, `[CountFromResult]`, `[HistogramFromResult]`, `[MetricTagFromResult]` and every `When` share one path resolver.

- **Paths start at the awaited return value.** For `Task<Result<T, E>>` that is the `Result`, so the success value is reached through `Value`, as in `Value.Usage.InputTokens`. For a task-like type marked `[AsyncMethodBuilder]`, such as `PooledTask<T>`, it is what `GetAwaiter().GetResult()` returns.
- **Each segment** names a property or field of the type reached so far, including members inherited from base types and interfaces.
- **The operator for each step** is chosen from the type: `?.` where the value can be null, `.` where it cannot. A `Value` segment on a nullable value type is dropped, since `?.` already unwraps it.
- **`dynamic`** ends the checking. Once a segment reaches a `dynamic` value, the rest of the path is emitted unchecked with `?.` and bound at run time, as 1.6.4 did. A tag or a `When` guard through `dynamic` compiles; a guard is compared with `== true`. `[CountFromResult]` and `[HistogramFromResult]` cannot check that a `dynamic` value fits the instrument, so they report **ZTEL009**; expose a typed member instead.
- **`When`** is a path to a `bool` or `bool?` member that must be `true`. The guard runs first and short-circuits, so the member is **not read at all** when it is false. That matters for a `Result` whose `Value` throws when unset.
- **A segment that names no readable, accessible instance property or field** is **ZTEL007**. It is reported at the argument, naming the segment and the type it was looked up on. A `When` that resolves to anything other than `bool` or `bool?` is **ZTEL008**. Both are errors, and neither attribute is emitted, so the diagnostic is the only error.
- **On a method with no return value** (`void`, `Task`, `ValueTask`, or a task-like type with no result), the result-reading attributes and `When` on `[Count]`/`[Histogram]` report **ZTEL005** and record nothing.

**Where the reads happen.** After the inner call, inside the same `try`, the result tags come first, then the result-driven instruments, then `[Count]` and `[Histogram]`, each with its metric tags. When the result can be null, all of them read one copy, `_tagged`. Null-testing `_result` itself would leave it maybe-null for the `return _result;` that follows, and that raises CS8603 in consumers with nullable warnings on.

---

## [TraceTag]

```csharp
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class TraceTagAttribute : Attribute
{
    public string Name { get; }
    public string? Member { get; }
    public TraceTagAttribute(string name);
    public TraceTagAttribute(string name, string member);
}
```

**Placement:** Parameter. Requires `[Trace]` on the same method.

**Effect:** Records the argument as a tag on the span, set immediately after the span starts — so it is present for the span's whole lifetime and visible to samplers that inspect tags at `ActivityStarted`.

```csharp
[Trace("vectorstore.search")]
Task<IReadOnlyList<SearchResult>> SearchAsync(
    [TraceTag("vectorstore.collection")] string collection,
    [TraceTag("top.k")] int topK,
    CancellationToken ct);
```

```csharp
// Generated:
using var _activity = _activitySource.StartActivity("vectorstore.search");
_activity?.SetTag("vectorstore.collection", collection);
_activity?.SetTag("top.k", topK);
```

### Tagging a member of an argument

The second overload takes a member path, which is often what you actually want — a count on a collection parameter, or an id nested inside a request object:

```csharp
[Trace("ingest.store")]
Task StoreAsync(
    [TraceTag("document.id", "DocumentId.Value")] DocumentMetadata metadata,
    [TraceTag("vectorstore.batch.size", "Count")] IReadOnlyList<EmbeddedChunk> chunks,
    CancellationToken ct);
```

```csharp
// Generated:
var _tag_metadata = metadata;
_activity?.SetTag("document.id", _tag_metadata?.DocumentId?.Value);
var _tag_chunks = chunks;
_activity?.SetTag("vectorstore.batch.size", _tag_chunks?.Count);
```

Every step is null-safe where the value can be null, so a null part-way along records a null tag instead of throwing. On a non-nullable value type there is nothing to test, so the access is plain and no copy is taken:

```csharp
[TraceTag("span.length", "Length")] ReadOnlyMemory<byte> buffer
// → _activity?.SetTag("span.length", buffer.Length);
```

The copy exists for a specific reason: the tagged argument is still forwarded to the wrapped call, and null-testing it directly would leave it maybe-null for the rest of the method — CS8604 in any consumer with nullable warnings enabled.

Paths follow the rules in [Member paths and When](#member-paths-and-when), except that they start at the parameter rather than the return value. A segment that names no readable, accessible instance property or field is **ZTEL010**, a warning reported at the argument. No tag is emitted for that parameter, so a typo such as `[TraceTag("batch.size", "Cuont")]` records nothing rather than the whole list under `batch.size`.

Without `[Trace]` there is no span to carry the tag, so the generator reports **ZTEL004** rather than silently dropping it.

---

## [TraceTagFromResult]

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class TraceTagFromResultAttribute : Attribute
{
    public string Name { get; }
    public string? Member { get; }
    public string? When { get; set; }
    public TraceTagFromResultAttribute(string name);
    public TraceTagFromResultAttribute(string name, string member);
}
```

**Placement:** Method. Requires `[Trace]` and a method that returns a value.

**Effect:** Records the return value, or a member of it, after the wrapped call returns. Counts are usually the interesting dimension, and they are only knowable afterwards.

```csharp
[Trace("vectorstore.search")]
[TraceTagFromResult("vectorstore.result.count", nameof(IReadOnlyList<SearchResult>.Count))]
Task<IReadOnlyList<SearchResult>> SearchAsync(string collection, CancellationToken ct);
```

For async methods the value comes from the **awaited result**, not the task. Omit `member` to record the whole result. The attribute may be applied more than once.

Member access is null-safe — a null result records a null tag rather than throwing. Instrumentation must never fail a call that would otherwise have succeeded.

On a method returning `void`, `Task` or `ValueTask` there is no result to read, so the generator reports **ZTEL005**.

A misspelt member or `When` is reported as **ZTEL007**, and a `When` that is not a boolean as **ZTEL008** — see [Member paths and When](#member-paths-and-when).

### Tagging only on one branch

`When` names a boolean member that must be true for the tag to be recorded. This is for results whose value is valid on one branch only — a `Result<T, E>` where the count lives at `Value.Count`:

```csharp
[Trace("ingest.chunk")]
[TraceTagFromResult("chunk.count", "Value.Count", When = "IsSuccess")]
Task<Result<IReadOnlyList<Chunk>, RagError>> ChunkAsync(CancellationToken ct);
```

```csharp
// Generated:
var _tagged = _result;
if (_tagged?.IsSuccess == true)
    _activity?.SetTag("chunk.count", _tagged?.Value?.Count);
```

The guard does something the null-conditional cannot: it stops the member being **read at all**. `?.` protects against a null *result*, not against a result whose value is unset — reading `Value` on a failed result is meaningless at best and throws at worst.

Any boolean member works, so this is not tied to any particular result type. The comparison against `true` is added only where the guard can be null; on a non-nullable value type the bare boolean is emitted:

```csharp
// ValueTask<Outcome> where Outcome is a struct with bool Ok
if (_result.Ok)
    _activity?.SetTag("outcome.count", _result.Count);
```

---

## [TraceTagConstant]

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class TraceTagConstantAttribute : Attribute
{
    public string Name { get; }
    public object? Value { get; }
    public TraceTagConstantAttribute(string name, object? value);
}
```

**Placement:** Method. Requires `[Trace]` on the same method.

**Effect:** Records a compile-time constant as a tag. For values that are simply *known* rather than derived from an argument or a return value — which implementation ran, which mode, which provider.

```csharp
[Trace("rerank.run")]
[TraceTagConstant("reranker.type", "CohereReranker")]
[TraceTagConstant("gen_ai.operation.name", "chat")]
Task<IReadOnlyList<RerankResult>> RerankAsync(CancellationToken ct);
```

```csharp
// Generated:
using var _activity = _activitySource.StartActivity("rerank.run");
_activity?.SetTag("reranker.type", "CohereReranker");
_activity?.SetTag("gen_ai.operation.name", "chat");
```

Any constant an attribute can carry works — string, bool, numeric, or enum:

| Written | Emitted |
|---|---|
| `"CohereReranker"` | `"CohereReranker"` |
| `true` | `true` |
| `42` | `42` |
| `SearchMode.Global` | `(global::SearchMode)2` |

Enums are emitted as a cast rather than by member name, which is also correct for combined flag values that have no single member to name.

Constants are emitted **before** argument tags, so they are the first thing present if a sampler inspects tags at `ActivityStarted`.

Two places this earns its keep:

- **Shared interfaces.** When several types implement one `[Instrument]` interface, a constant tag is how their spans are told apart.
- **OTel semantic conventions**, which mandate several constant-valued attributes — `gen_ai.operation.name`, `gen_ai.provider.name`, `db.system.name`. There is no other way to express them.

Without `[Trace]` there is no span to carry the tag, so the generator reports **ZTEL004**.

---

## What tags cost

Nothing, unless someone is listening.

The generated call is `_activity?.SetTag(...)`. When no listener sampled the span, `StartActivity` returns null and the null-conditional operator skips the entire invocation — including evaluating the argument. Since `SetTag` takes `object?`, value-typed tags box; that boxing therefore only happens on spans that are actually being recorded.

Sampling is the control: an unsampled call pays for neither the tag nor its boxing.

---

## Tags the proxy cannot see

A proxy can only observe what crosses the method boundary — arguments and the return value. Anything computed *inside* the implementation is invisible to it.

For those, set the tag directly on the ambient activity. `Activity.Current` inside the wrapped implementation **is** the span the proxy started, so tags attach to it:

```csharp
public async Task<IReadOnlyList<SearchResult>> SearchAsync(string collection, CancellationToken ct)
{
    var candidates = await _index.QueryAsync(collection, ct);
    Activity.Current?.SetTag("vectorstore.candidate.count", candidates.Count);
    return Rerank(candidates);
}
```

This composes with `[TraceTag]` — use the attributes for boundary values and `Activity.Current` for computed state, rather than choosing between them.

---

## Sharing an ActivitySource across assemblies

Two assemblies that each generate a proxy with the **same** `[Instrument]` name both produce spans observed by a single `AddSource(name)` listener — the generated `ActivitySource` instances are distinct objects but share a name, which is what listeners match on.

Generated spans also nest correctly under a parent started manually in a different assembly, because parenting flows through `Activity.Current` rather than through the source.

Both properties are relied upon by design; neither requires the proxies to share an assembly.

---

## Combining Attributes

All the attributes can appear on the same method:

```csharp
[Instrument("MyApp.Payments")]
public interface IPaymentGateway
{
    [Trace("payment.charge")]
    [Count("payments.charged")]
    [Histogram("payment.charge_ms")]
    [TraceTagFromResult("payment.status", nameof(ChargeResult.Status))]
    ValueTask<ChargeResult> ChargeAsync(
        [TraceTag("payment.method")] ChargeRequest request,
        CancellationToken ct);
}
```

The generated code records the span with its tags, the histogram (on both success and failure), and the counter (on success only).

---

## Methods Without Attributes

Methods with no `[Trace]`, `[Count]`, `[Histogram]`, `[CountFromResult]` or `[HistogramFromResult]` annotation are passed through to the inner implementation without any wrapping — no try/catch, no timing, no span. They are still correctly proxied.
