---
id: getting-started
title: Getting Started
slug: /docs/getting-started
description: Install ZeroAlloc.Telemetry and instrument your first interface in under five minutes.
sidebar_position: 2
---

# Getting Started

## Installation

```bash
dotnet add package ZeroAlloc.Telemetry
```

The package bundles the attribute assembly and the Roslyn source generator. No separate generator install is required.

**Minimum requirements:** .NET 8, C# 12

## Step 1 — Annotate your interface

Apply `[Instrument]` to the interface you want to instrument. Provide the `ActivitySource` name — this becomes the `ActivitySource` and `Meter` name in the generated proxy.

```csharp
using ZeroAlloc.Telemetry;

[Instrument("MyApp.Orders")]
public interface IOrderService
{
    [Trace("order.create")]
    [Count("orders.created")]
    ValueTask<OrderId> CreateOrderAsync(CreateOrderCommand cmd, CancellationToken ct);

    [Trace("order.get")]
    [Histogram("order.get_ms")]
    ValueTask<Order> GetOrderAsync(OrderId id, CancellationToken ct);

    // Methods without attributes are passed through uninstrumented.
    ValueTask DeleteOrderAsync(OrderId id, CancellationToken ct);
}
```

## Step 2 — Build

Run `dotnet build`. The generator discovers `[Instrument]`-annotated interfaces and emits a proxy class in your project's namespace:

- `IOrderService` → `OrderServiceInstrumented`
- `IPaymentGateway` → `PaymentGatewayInstrumented`
- Naming rule: strip the leading `I`, append `Instrumented`

The generated file is compiled automatically — it lives in `obj/` and does not need to be checked in.

## Step 3 — Wire up in DI

The generated proxy wraps your existing implementation. Register it as a decorator:

```csharp
services.AddSingleton<OrderServiceImpl>();
services.AddSingleton<IOrderService>(sp =>
    new OrderServiceInstrumented(sp.GetRequiredService<OrderServiceImpl>()));
```

## Step 4 — Add an exporter (application concern)

ZeroAlloc.Telemetry emits BCL `ActivitySource` and `Meter` — no OpenTelemetry SDK is included. Add exporters in your application startup:

```bash
dotnet add package OpenTelemetry.Exporter.Console
```

```csharp
// Program.cs
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddSource("MyApp.Orders")
        .AddConsoleExporter())
    .WithMetrics(metrics => metrics
        .AddMeter("MyApp.Orders")
        .AddConsoleExporter());
```

The source name (`"MyApp.Orders"`) must match what you passed to `[Instrument]`.

## What's recorded

| Call outcome | `[Trace]` span | `[Count]` counter | `[Histogram]` |
|---|---|---|---|
| Success | Started + stopped | Incremented by 1 | Records elapsed ms |
| Exception | Started + Error status | Not incremented | Records elapsed ms |
| No attribute | — | — | — |

## Next steps

- [Attribute Reference](attributes.md) — all four attributes with generated output
- [Source Generator](source-generator.md) — full generated class layout
- [Testing](testing.md) — assert spans and metrics in tests without an exporter
