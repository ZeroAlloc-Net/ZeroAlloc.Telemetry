# ZeroAlloc.Telemetry

[![NuGet](https://img.shields.io/nuget/v/ZeroAlloc.Telemetry.svg)](https://www.nuget.org/packages/ZeroAlloc.Telemetry)
[![Build](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/actions/workflows/ci.yml/badge.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/actions/workflows/ci.yml)
[![codecov](https://codecov.io/gh/ZeroAlloc-Net/ZeroAlloc.Telemetry/graph/badge.svg)](https://codecov.io/gh/ZeroAlloc-Net/ZeroAlloc.Telemetry)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/main/LICENSE)
[![AOT](https://img.shields.io/badge/AOT--Compatible-passing-brightgreen)](https://learn.microsoft.com/dotnet/core/deploying/native-aot/)
[![GitHub Sponsors](https://img.shields.io/github/sponsors/MarcelRoozekrans?style=flat&logo=githubsponsors&color=ea4aaa&label=Sponsor)](https://github.com/sponsors/MarcelRoozekrans)

Put `[Instrument]` on an interface and get OpenTelemetry spans and metrics for every call, generated at compile time: no hand-written `ActivitySource` boilerplate, no reflection, and nothing allocated when nobody is listening.

## Why ZeroAlloc.Telemetry

- **0 B per call when nothing listens**, the common production case after sampling. Tests assert it for `Task`, `ValueTask` and `ValueTask<T>` methods, tagged metrics included.
- **7.5× faster than a straightforward hand-written `ActivitySource` + `Meter` wrapper** with no listener attached, measured in the same run: the proxy skips the instrumented path entirely until something subscribes.
- **Works under NativeAOT and trimming.** Plain generated code over BCL `ActivitySource` and `Meter`, no reflection and no `params object[]`; CI publishes a NativeAOT app.
- **Mistakes surface at compile time.** A result member that is not numeric, or a metric attribute on an interface without `[Instrument]`, is a compiler diagnostic.

## Install

```bash
dotnet add package ZeroAlloc.Telemetry
```

No OpenTelemetry SDK dependency: add the exporters you want, OTLP, Prometheus or Console, in your application.

## Quick start

```csharp
using System.Diagnostics;
using ZeroAlloc.Telemetry;

using var listener = new ActivityListener
{
    ShouldListenTo = source => source.Name == "MyApp.Orders",
    Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
    ActivityStopped = activity => Console.WriteLine($"span {activity.DisplayName}: {activity.Duration.TotalMilliseconds:F1} ms"),
};
ActivitySource.AddActivityListener(listener);

IOrderService orders = new OrderServiceInstrumented(new OrderService()); // generated proxy
await orders.PlaceOrderAsync("book", CancellationToken.None);            // prints the order.place span

[Instrument("MyApp.Orders")]
public interface IOrderService
{
    [Trace("order.place")]
    [Count("orders.placed")]
    [Histogram("order.place_ms")]
    ValueTask<int> PlaceOrderAsync(string product, CancellationToken ct);
}

public sealed class OrderService : IOrderService
{
    public async ValueTask<int> PlaceOrderAsync(string product, CancellationToken ct)
    {
        await Task.Delay(10, ct);
        return 42;
    }
}
```

In an application, register the proxy in DI in place of the implementation, and subscribe with OpenTelemetry's `AddSource("MyApp.Orders")` and `AddMeter("MyApp.Orders")`. Your code keeps calling `IOrderService`.

## What you get

- **Spans** per method, with error status and `error.type` on exceptions, or on a failed `Result` you choose to treat as an error.
- **Counters and duration histograms** per method, recorded on success or on every call, optionally only when a result guard such as `IsSuccess` holds.
- **Metrics from the return value**, such as tokens consumed or a cost, recorded straight from a member of the result.
- **Tags** on spans and metrics from arguments, from the result, or as constants, built only when a listener needs them.
- **Consistent instrumentation**: every method gets the same span, counter and histogram pattern, with no Activity left undisposed and no metric forgotten.
- **Testable with BCL listeners**, no exporter needed: [Testing](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/main/docs/testing.md).
- Every attribute and option: [attribute reference](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/main/docs/attributes.md). Compile-time diagnostics: [the full list](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/main/docs/source-generator.md#diagnostics).

## Performance

No listener attached, the production case after sampling. .NET 10.0.13, BenchmarkDotNet v0.15.8. Time is relative to the uninstrumented call in the same run; allocations are exact.

| Method | Time vs direct call | Allocated |
|---|---:|---:|
| Direct call, no instrumentation | 1× | 0 B |
| Hand-written `ActivitySource` + `Counter` + `Histogram` | 60.6× | 0 B |
| **ZA.Telemetry generated proxy** | **8.1×** | **0 B** |

Neither wrapper allocates. The hand-written version still runs its `async` state machine, takes timestamps and calls instruments nobody listens to; the proxy checks for listeners and hands back the inner task as is. Hand-written code can do the same, but every method has to remember to. Methodology: [docs/performance.md](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/main/docs/performance.md).

## Learn more

- Documentation: [telemetry.zeroalloc.net](https://telemetry.zeroalloc.net)
- [Getting started](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/main/docs/getting-started.md), [what the generator emits](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/main/docs/source-generator.md) and [AOT and trimming](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/main/docs/aot.md)
- Related packages: [ZeroAlloc.Cache](https://github.com/ZeroAlloc-Net/ZeroAlloc.Cache) and [ZeroAlloc.Resilience](https://github.com/ZeroAlloc-Net/ZeroAlloc.Resilience), proxies generated the same way; [ZeroAlloc.Results](https://github.com/ZeroAlloc-Net/ZeroAlloc.Results) for `Result`-aware metrics
- Contributing: issues and pull requests are welcome on [GitHub](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/issues)
- License: [MIT](https://github.com/ZeroAlloc-Net/ZeroAlloc.Telemetry/blob/main/LICENSE)
