---
id: performance
title: Performance
slug: /docs/performance
description: Allocation and latency profile of the generated instrumentation proxy.
sidebar_position: 7
---

# Performance

The value proposition of ZeroAlloc.Telemetry is instrumentation that is cheap enough to leave enabled in production even when nobody is listening. The common case for a trace-heavy service is that a sampler drops most spans — in that world, the per-call cost of *not* observing a span is what drives the aggregate overhead. This page covers the design decisions that keep the no-listeners path near-free, and the benchmark that validates the claim.

## Why most instrumentation libraries allocate

Typical OpenTelemetry adoption patterns route every call through a reflection- or DynamicProxy-based interceptor:

```csharp
// Reflection-based wrapper — allocates a MethodInfo lookup, ParameterInfo[],
// and a tags dictionary per call.
public T Call<T>(string operationName, object[] args, Func<T> body) { ... }
```

Three allocation sources stack up:

- **Tag dictionaries**: every call builds a `Dictionary<string, object?>` to attach request attributes, even when no `ActivityListener` is subscribed
- **`params object[]`**: tag values are boxed as `object` into an array created per call
- **Closure captures**: the `Func<T>` body captures state in a compiler-generated closure

Under load each of these lights up the GC. The instrumented path can cost more than the work being instrumented.

## How ZeroAlloc.Telemetry eliminates it

The generator emits a fully concrete proxy class per `[Instrument]`-annotated interface. Key decisions:

**1. Pre-resolved `static readonly ActivitySource` and `Meter`**

```csharp
internal sealed class OrderServiceInstrumented : IOrderService
{
    private static readonly ActivitySource _source = new("MyApp.Orders");
    private static readonly Counter<long> _created  = _meter.CreateCounter<long>("orders.created");
    private static readonly Histogram<double> _durationMs = _meter.CreateHistogram<double>("order.create_ms");
    // ...
}
```

One allocation per type at startup. Zero per call.

**2. Cheap early-return when no listeners**

`ActivitySource.StartActivity(name)` returns `null` when no listener is subscribed — the emitted code guards on this and skips all tag-attachment work:

```csharp
var activity = _source.StartActivity("order.create");
try
{
    return await _inner.CreateOrderAsync(request, ct).ConfigureAwait(false);
}
finally
{
    activity?.Dispose();
}
```

For a method returning `Task`, `ValueTask` or another awaitable, the proxy goes one step further. Its public method is not `async`: when the source has no listener and none of the method's instruments is enabled, it returns the inner task as is, so no state machine is allocated even when the inner call completes asynchronously. Only when something would record the call does it go through an `async` core. An exception the inner method throws before returning its task still reaches the caller inside the returned task, so failures look the same with telemetry on or off; only that exception path allocates:

```csharp
public ValueTask<OrderId> CreateOrderAsync(CreateOrderCommand cmd, CancellationToken ct)
{
    if (!_activitySource.HasListeners() && !_orders_created.Enabled)
    {
        try
        {
            return _inner.CreateOrderAsync(cmd, ct);
        }
        catch (Exception _ex)
        {
            return _fault_CreateOrderAsync_0(_ex);
        }
    }
    return _core_CreateOrderAsync_0(cmd, ct);
}
```

Counter/histogram increments go through `Counter<long>.Add(...)` and `Histogram<double>.Record(...)` — these are `Interlocked`-backed on a single field, no per-call allocation.

**3. No `params object[]`**

Tags, when present, go into a `TagList` struct on the stack, never a `params` array or a dictionary, and the generator knows the tag names at compile time. A tag value is an `object?`, so a value-type tag value boxes; strings do not. That happens only when someone is listening: the tag list is built inside the check of the instrument's `Enabled` or the span's existence, so the unsampled path allocates nothing. See [what tags cost](attributes.md#what-tags-cost) for spans and [`[MetricTagFromResult]`](attributes.md#metrictagfromresult) for metrics.

## Head-to-head vs hand-written ActivitySource

<!-- BENCH:START -->
_Last refreshed: 2026-10-11_

The realistic alternative to ZA.Telemetry's generator is hand-written `ActivitySource` + `Meter` wrapping — wrapping every instrumented method with `using var activity = source.StartActivity(...); counter.Add(1); histogram.Record(...)`. This benchmark compares the two in the no-listeners profile (the common production case after sampling).

.NET 10.0.12, i9-12900HK, BenchmarkDotNet v0.15.8. The benchmark methods return the service's `ValueTask<int>` directly, so the rows show only what each wrapper adds.

| Method | Time | Allocated |
|---|---:|---:|
| Direct call (no instrumentation) | 3.2 ns | 0 B |
| Hand-written `ActivitySource` + `Counter` + `Histogram` | 97.7 ns | 0 B |
| **ZA.Telemetry generated proxy** | **14.6 ns** | **0 B** |

Neither wrapper allocates in the no-listeners path. The generated proxy adds about 11 ns over the direct call, against about 95 ns for the straightforward hand-written wrapper. The difference is the short-circuit described above: with no listener on the source and no instrument enabled, the proxy returns the inner `ValueTask` as is, while the hand-written version still runs its `async` state machine, takes two `Stopwatch` timestamps, and calls `Counter.Add` and `Histogram.Record` on instruments nobody is listening to. Hand-written code can do the same, but every method has to remember to.

**The takeaway**: ZA.Telemetry's value isn't faster instrumentation than hand-writing it — it's eliminating the boilerplate so every instrumented method gets the same try/finally + counter + histogram pattern, with zero risk of forgetting to dispose an Activity or skipping a metric.
<!-- BENCH:END -->

## Self-benchmark

The [benchmarks/ZeroAlloc.Telemetry.Benchmarks](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/tree/main/benchmarks/ZeroAlloc.Telemetry.Benchmarks) project also contains `InstrumentedProxyBenchmark`. It compares:

- **Baseline**: direct call on the underlying `IOrderService` implementation
- **Proxy (no listeners)**: the generator-emitted `OrderServiceInstrumented` with no `ActivityListener` subscribed — the production-common path when tracing is sampled away

The claim to verify: the proxy path allocates `0 B/op` when no listener is attached, matching the baseline byte-for-byte. The latency delta measures the `HasListeners` and `Enabled` checks the proxy makes before handing the call to the inner service.

Measured on 2026-10-11, .NET 10.0.12, i9-12900HK, BenchmarkDotNet v0.15.8:

| Benchmark | Mean | Allocated |
|---|---:|---:|
| direct | 3.6 ns | 0 B |
| proxy (no listeners) | 15.2 ns | 0 B |

### Run the benchmark

```bash
dotnet run --project benchmarks/ZeroAlloc.Telemetry.Benchmarks -c Release --filter "*"
```

Results are written to `benchmarks/ZeroAlloc.Telemetry.Benchmarks/BenchmarkDotNet.Artifacts/results/`.

### What to watch

- **Allocated column**: both rows must read `0 B`. A regression here points at a new tag-capture path that escaped into a boxed value or dictionary.
- **Ratio column**: the proxy row's Mean should stay within a small constant overhead over the baseline, about 12 ns on the machine above.

## When listeners ARE subscribed

Once a sampler selects a span and an `ActivityListener.Sample` returns `AllData`, the Activity object is allocated by the BCL. That allocation is intrinsic to Activity itself — ZeroAlloc.Telemetry doesn't claim to eliminate it. The promise is that the **unsampled** path — which dominates the aggregate workload in production — stays allocation-free.

## Aggregate impact

For a service handling 10,000 req/s with instrumentation on every request, a reflection-based wrapper at ~1 KB/call allocates roughly 10 MB/s. At default GC settings this forces a Gen0 collection every ~250 ms. ZeroAlloc.Telemetry's proxy path takes this to 0 B/s on the unsampled path — collections are driven by the actual application workload, not the tracing overhead.
