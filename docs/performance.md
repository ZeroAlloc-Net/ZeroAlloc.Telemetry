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
    return await _inner.CreateOrderAsync(request, ct);
}
finally
{
    activity?.Dispose();
}
```

Counter/histogram increments go through `Counter<long>.Add(...)` and `Histogram<double>.Record(...)` — these are `Interlocked`-backed on a single field, no per-call allocation.

**3. No `params object[]`**

Tags, when present, are emitted as positional `TagList` struct values — allocated on the stack, never boxed. The generator knows the tag names and value types at compile time.

## Benchmark

The [benchmarks/ZeroAlloc.Telemetry.Benchmarks](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/tree/main/benchmarks/ZeroAlloc.Telemetry.Benchmarks) project contains a single representative measurement: `InstrumentedProxyBenchmark`. It compares:

- **Baseline**: direct call on the underlying `IOrderService` implementation
- **Proxy (no listeners)**: the generator-emitted `OrderServiceInstrumented` with no `ActivityListener` subscribed — the production-common path when tracing is sampled away

The claim to verify: the proxy path allocates `0 B/op` when no listener is attached, matching the baseline byte-for-byte. The latency delta measures pure Activity-source-null-check + Meter-add overhead.

### Run the benchmark

```bash
dotnet run --project benchmarks/ZeroAlloc.Telemetry.Benchmarks -c Release --filter "*"
```

Results are written to `benchmarks/ZeroAlloc.Telemetry.Benchmarks/BenchmarkDotNet.Artifacts/results/`.

### What to watch

- **Allocated column**: both rows must read `0 B`. A regression here points at a new tag-capture path that escaped into a boxed value or dictionary.
- **Ratio column**: the proxy row's Mean should stay within a small constant overhead (typically under 10 ns) over the baseline.

## When listeners ARE subscribed

Once a sampler selects a span and an `ActivityListener.Sample` returns `AllData`, the Activity object is allocated by the BCL. That allocation is intrinsic to Activity itself — ZeroAlloc.Telemetry doesn't claim to eliminate it. The promise is that the **unsampled** path — which dominates the aggregate workload in production — stays allocation-free.

## Aggregate impact

For a service handling 10,000 req/s with instrumentation on every request, a reflection-based wrapper at ~1 KB/call allocates roughly 10 MB/s. At default GC settings this forces a Gen0 collection every ~250 ms. ZeroAlloc.Telemetry's proxy path takes this to 0 B/s on the unsampled path — collections are driven by the actual application workload, not the tracing overhead.
