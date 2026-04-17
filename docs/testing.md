---
id: testing
title: Testing
slug: /docs/testing
description: Test instrumented code with BCL ActivityListener and MeterListener — no OpenTelemetry SDK or exporter required.
sidebar_position: 5
---

# Testing

ZeroAlloc.Telemetry uses BCL `ActivitySource` and `Meter` — no OpenTelemetry SDK is required to verify spans and metrics in tests. The BCL provides `ActivityListener` and `MeterListener` for exactly this purpose.

## Asserting Activity Spans

```csharp
using System.Diagnostics;

public sealed class OrderServiceInstrumentedTests : IDisposable
{
    private readonly List<Activity> _startedActivities = [];
    private readonly ActivityListener _listener;

    public OrderServiceInstrumentedTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == "MyApp.Orders",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => _startedActivities.Add(a),
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public async Task CreateOrderAsync_StartsActivity()
    {
        var inner = Substitute.For<IOrderService>();
        inner.CreateOrderAsync(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>())
             .Returns(ValueTask.FromResult(new OrderId(Guid.NewGuid())));

        var sut = new OrderServiceInstrumented(inner);
        await sut.CreateOrderAsync(new CreateOrderCommand(...), CancellationToken.None);

        _startedActivities.Should().ContainSingle(a => a.OperationName == "order.create");
    }

    [Fact]
    public async Task CreateOrderAsync_SetsErrorStatus_OnException()
    {
        var inner = Substitute.For<IOrderService>();
        inner.CreateOrderAsync(Arg.Any<CreateOrderCommand>(), Arg.Any<CancellationToken>())
             .ThrowsAsync(new InvalidOperationException("store unavailable"));

        var sut = new OrderServiceInstrumented(inner);
        var act = async () => await sut.CreateOrderAsync(new CreateOrderCommand(...), CancellationToken.None);
        await act.Should().ThrowAsync<InvalidOperationException>();

        var activity = _startedActivities.Should().ContainSingle(a => a.OperationName == "order.create").Subject;
        activity.Status.Should().Be(ActivityStatusCode.Error);
    }
}
```

**Key points:**
- `ShouldListenTo` filters by `ActivitySource` name — must match what you passed to `[Instrument]`
- `Sample` must return `AllDataAndRecorded` to capture tags and status; `AllData` suffices for most assertions
- `ActivityStarted` fires synchronously — the activity is in the list when your `await` returns
- Dispose the `ActivityListener` in `Dispose()` to prevent cross-test pollution

## Asserting Counters

```csharp
using System.Collections.Concurrent;
using System.Diagnostics.Metrics;

public sealed class OrderServiceInstrumentedTests : IDisposable
{
    private readonly ConcurrentBag<(string Name, long Value)> _counterMeasurements = [];
    private readonly MeterListener _meterListener;

    public OrderServiceInstrumentedTests()
    {
        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "MyApp.Orders")
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            _counterMeasurements.Add((instrument.Name, value)));
        _meterListener.Start();
    }

    public void Dispose() => _meterListener.Dispose();

    [Fact]
    public async Task CreateOrderAsync_IncrementsCounter_OnSuccess()
    {
        // ... arrange and act ...

        _counterMeasurements
            .Should().ContainSingle(m => m.Name == "orders.created" && m.Value == 1);
    }

    [Fact]
    public async Task CreateOrderAsync_DoesNotIncrementCounter_OnException()
    {
        // ... arrange with throwing inner ...

        _counterMeasurements
            .Where(m => m.Name == "orders.created")
            .Should().BeEmpty();
    }
}
```

**Key points:**
- `InstrumentPublished` decides which instruments to enable — filter by `Meter.Name`
- Use `ConcurrentBag<T>` for measurement accumulation: the callback fires on whichever thread `Add`/`Record` was called from
- `RecordObservableInstruments()` is needed only for observable instruments (gauges); regular `Counter<T>` and `Histogram<T>` fire the callback immediately on each `Add`/`Record` call

## Asserting Histograms

```csharp
private readonly ConcurrentBag<(string Name, double Value)> _histogramMeasurements = [];

// In MeterListener setup:
_meterListener.SetMeasurementEventCallback<double>((instrument, value, _, _) =>
    _histogramMeasurements.Add((instrument.Name, value)));

// Assert:
_histogramMeasurements
    .Should().ContainSingle(m => m.Name == "order.get_ms" && m.Value >= 0);
```

Histograms record on both success and exception — verify both paths if the guarantee matters.

## Combining ActivityListener and MeterListener

Both listeners can be set up in the same test class. Initialise both in the constructor and dispose both in `Dispose()`:

```csharp
public sealed class MyTests : IDisposable
{
    private readonly List<Activity> _startedActivities = [];
    private readonly ConcurrentBag<(string Name, long Value)> _counterMeasurements = [];
    private readonly ActivityListener _activityListener;
    private readonly MeterListener _meterListener;

    public MyTests()
    {
        _activityListener = new ActivityListener
        {
            ShouldListenTo = src => src.Name == "MyApp.Orders",
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = a => _startedActivities.Add(a),
        };
        ActivitySource.AddActivityListener(_activityListener);

        _meterListener = new MeterListener
        {
            InstrumentPublished = (instrument, listener) =>
            {
                if (instrument.Meter.Name == "MyApp.Orders")
                    listener.EnableMeasurementEvents(instrument);
            }
        };
        _meterListener.SetMeasurementEventCallback<long>((instrument, value, _, _) =>
            _counterMeasurements.Add((instrument.Name, value)));
        _meterListener.Start();
    }

    public void Dispose()
    {
        _activityListener.Dispose();
        _meterListener.Dispose();
    }
}
```

## No Exporter Required

The test setup above requires no NuGet packages beyond `xunit`, `FluentAssertions`, and `NSubstitute`. Everything comes from the BCL. There is no need to configure an OpenTelemetry SDK, OTLP endpoint, or in-memory exporter.
