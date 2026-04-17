---
id: index
title: ZeroAlloc.Telemetry
slug: /
description: Source-generated OpenTelemetry instrumentation — Activity spans and Meter instruments without reflection or allocation. Native AOT safe.
sidebar_position: 1
---

# ZeroAlloc.Telemetry

Source-generated OpenTelemetry instrumentation for .NET.

Add `[Instrument]` to any interface and the generator emits a sealed proxy class that records `Activity` spans and `Meter` instruments per method — without reflection, `params object[]` boxing, or runtime attribute inspection.

---

## Contents

| Page | Description |
|---|---|
| [Getting Started](getting-started.md) | Install and instrument your first interface |
| [Attribute Reference](attributes.md) | `[Instrument]`, `[Trace]`, `[Count]`, `[Histogram]` |
| [Source Generator](source-generator.md) | What the generator emits — input/output examples |
| [Testing](testing.md) | Assert spans and metrics with BCL listeners, no exporter needed |
| [AOT & Trimming](aot.md) | Native AOT compatibility |

---

## Quick Example

```csharp
[Instrument("MyApp.Orders")]
public interface IOrderService
{
    [Trace("order.create")]
    [Count("orders.created")]
    ValueTask<OrderId> CreateOrderAsync(CreateOrderCommand cmd, CancellationToken ct);

    [Trace("order.get")]
    [Histogram("order.get_ms")]
    ValueTask<Order> GetOrderAsync(OrderId id, CancellationToken ct);
}
```

The generator emits `OrderServiceInstrumented : IOrderService`. Wrap your implementation at the DI layer:

```csharp
services.AddSingleton<OrderServiceImpl>();
services.AddSingleton<IOrderService>(sp =>
    new OrderServiceInstrumented(sp.GetRequiredService<OrderServiceImpl>()));
```

That's all. No OpenTelemetry SDK is required — `ActivitySource` and `Meter` are BCL types (`net8.0+`). Add exporters in your application startup independently.

---

## Instruments at a Glance

| Attribute | What it records | When |
|---|---|---|
| `[Trace("name")]` | `Activity` span | Every call — Error status on exception |
| `[Count("metric")]` | `Counter<long>` +1 | Success only |
| `[Histogram("metric")]` | `Histogram<double>` elapsed ms | Every call including on exception |
