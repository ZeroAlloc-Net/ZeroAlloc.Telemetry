---
id: attributes
title: Attribute Reference
slug: /docs/attributes
description: Reference for [Instrument], [Trace], [Count], [Histogram], [TraceTag], and [TraceTagFromResult] — the attributes in ZeroAlloc.Telemetry.
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
    public InstrumentAttribute(string activitySource);
}
```

**Placement:** Interface only.

**Effect:** Triggers the source generator. The generator emits a sealed proxy class named `{TypeName}Instrumented` (leading `I` stripped) in the same namespace as the interface.

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

---

## [Trace]

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class TraceAttribute : Attribute
{
    public string Name { get; }
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

---

## [Count]

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class CountAttribute : Attribute
{
    public string Metric { get; }
    public CountAttribute(string metric);
}
```

**Placement:** Interface method.

**Effect:** Increments a `Counter<long>` by 1 after a successful (non-throwing) call only.

The counter field is a static field on the proxy — one per unique metric name across all methods. If two methods share the same metric name, only one `Counter<long>` field is emitted.

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

---

## [Histogram]

```csharp
[AttributeUsage(AttributeTargets.Method)]
public sealed class HistogramAttribute : Attribute
{
    public string Metric { get; }
    public HistogramAttribute(string metric);
}
```

**Placement:** Interface method.

**Effect:** Records the elapsed time in milliseconds in a `Histogram<double>` on every call — including when the method throws.

Uses `Stopwatch.GetTimestamp()` before the call and `Stopwatch.GetElapsedTime(ts).TotalMilliseconds` after, so the measurement includes the full method duration regardless of outcome.

```csharp
[Histogram("payment.charge_ms")]
ValueTask<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct);
```

Generated field + recording:
```csharp
private static readonly Histogram<double> _payment_charge_ms =
    _meter.CreateHistogram<double>("payment.charge_ms");

// In the method body:
var _sw = Stopwatch.GetTimestamp();
try
{
    var _result = await _inner.ChargeAsync(request, ct);
    _payment_charge_ms.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);
    return _result;
}
catch (Exception _ex)
{
    _payment_charge_ms.Record(Stopwatch.GetElapsedTime(_sw).TotalMilliseconds);
    throw;
}
```

---

## [TraceTag]

```csharp
[AttributeUsage(AttributeTargets.Parameter)]
public sealed class TraceTagAttribute : Attribute
{
    public string Name { get; }
    public TraceTagAttribute(string name);
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

Without `[Trace]` there is no span to carry the tag, so the generator reports **ZTEL004** rather than silently dropping it.

---

## [TraceTagFromResult]

```csharp
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
public sealed class TraceTagFromResultAttribute : Attribute
{
    public string Name { get; }
    public string? Member { get; }
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

Methods with no `[Trace]`, `[Count]`, or `[Histogram]` annotation are passed through to the inner implementation without any wrapping — no try/catch, no timing, no span. They are still correctly proxied.
