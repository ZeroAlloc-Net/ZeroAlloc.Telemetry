---
id: aot
title: AOT & Trimming
slug: /docs/aot
description: Native AOT and IL trimming compatibility in ZeroAlloc.Telemetry.
sidebar_position: 6
---

# AOT & Trimming

## Generated Code

Generated proxy classes are fully AOT-safe:

- The interface type is resolved at generation time — no open-generic reflection at runtime
- `ActivitySource` and `Meter` are constructed via plain `new ActivitySource("name")` and `new Meter("name")` calls — no reflection
- All instrument fields (`Counter<long>`, `Histogram<double>`) are created via direct method calls on `Meter` — no dynamic dispatch
- No `params object[]` spans, no `[RequiresDynamicCode]` calls, no `Type.GetMethod` anywhere in the generated output

## BCL Telemetry

`System.Diagnostics.ActivitySource`, `System.Diagnostics.Activity`, `System.Diagnostics.Metrics.Meter`, `Counter<T>`, and `Histogram<T>` are all BCL types — fully supported in Native AOT since .NET 8.

## No OpenTelemetry SDK Dependency

ZeroAlloc.Telemetry has **no dependency** on OpenTelemetry SDK NuGet packages. Exporters (OTLP, Console, Prometheus, etc.) are the application's concern and are independent of whether the application uses Native AOT.

Check each exporter package's own AOT documentation before publishing. `OpenTelemetry.Exporter.OpenTelemetryProtocol` is AOT-compatible from version 1.9+.

## Publishing

```xml
<!-- .csproj -->
<PublishAot>true</PublishAot>
```

No additional configuration is needed for the generated proxy code. If you use an OpenTelemetry exporter, verify its AOT compatibility separately.
