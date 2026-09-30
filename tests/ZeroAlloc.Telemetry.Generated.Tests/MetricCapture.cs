using System.Diagnostics.Metrics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>Records every measurement published by one meter, by instrument name.</summary>
public sealed class MetricCapture : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly Lock _gate = new();
    private readonly List<Measurement> _measurements = [];
    private readonly Dictionary<string, Instrument> _instruments = new(StringComparer.Ordinal);

    public MetricCapture(string meterName)
    {
        _listener.InstrumentPublished = (instrument, listener) =>
        {
            if (!string.Equals(instrument.Meter.Name, meterName, StringComparison.Ordinal))
                return;

            lock (_gate)
                _instruments[instrument.Name] = instrument;

            listener.EnableMeasurementEvents(instrument);
        };
        _listener.SetMeasurementEventCallback<long>((instrument, value, tags, _) => Add(instrument.Name, value, tags));
        _listener.SetMeasurementEventCallback<double>((instrument, value, tags, _) => Add(instrument.Name, value, tags));
        _listener.Start();
    }

    public IReadOnlyList<double> ValuesOf(string instrument)
    {
        lock (_gate)
        {
            return _measurements
                .Where(m => string.Equals(m.Instrument, instrument, StringComparison.Ordinal))
                .Select(m => m.Value)
                .ToList();
        }
    }

    /// <summary>The tags of each measurement of <paramref name="instrument"/>, in recording order.</summary>
    public IReadOnlyList<IReadOnlyDictionary<string, object?>> TagsOf(string instrument)
    {
        lock (_gate)
        {
            return _measurements
                .Where(m => string.Equals(m.Instrument, instrument, StringComparison.Ordinal))
                .Select(m => m.Tags)
                .ToList();
        }
    }

    public Instrument Published(string instrument)
    {
        lock (_gate)
            return _instruments[instrument];
    }

    public void Dispose() => _listener.Dispose();

    private void Add(string instrument, double value, ReadOnlySpan<KeyValuePair<string, object?>> tags)
    {
        // The span is only valid during the callback, so the tags are copied out.
        var copy = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (ref readonly var tag in tags)
            copy.Add(tag.Key, tag.Value);

        lock (_gate)
            _measurements.Add(new Measurement(instrument, value, copy));
    }

    private sealed record Measurement(string Instrument, double Value, IReadOnlyDictionary<string, object?> Tags);
}
