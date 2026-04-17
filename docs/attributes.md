---
id: attributes
title: Attribute Reference
slug: /docs/attributes
description: Reference for [Instrument], [Trace], [Count], and [Histogram] — the four attributes in ZeroAlloc.Telemetry.
sidebar_position: 3
---

# Attribute Reference

## [Instrument]

```csharp
[AttributeUsage(AttributeTargets.Interface)]
public sealed class InstrumentAttribute : Attribute
{
    public string ActivitySource { get; }
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

## Combining Attributes

All four attributes can appear on the same method:

```csharp
[Instrument("MyApp.Payments")]
public interface IPaymentGateway
{
    [Trace("payment.charge")]
    [Count("payments.charged")]
    [Histogram("payment.charge_ms")]
    ValueTask<ChargeResult> ChargeAsync(ChargeRequest request, CancellationToken ct);
}
```

The generated code records the span, the histogram (on both success and failure), and the counter (on success only).

---

## Methods Without Attributes

Methods with no `[Trace]`, `[Count]`, or `[Histogram]` annotation are passed through to the inner implementation without any wrapping — no try/catch, no timing, no span. They are still correctly proxied.
