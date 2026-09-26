using System.Diagnostics.Metrics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>Records every measurement published by one meter, by instrument name.</summary>
public sealed class MetricCapture : IDisposable
{
    private readonly MeterListener _listener = new();
    private readonly Lock _gate = new();
    private readonly List<KeyValuePair<string, double>> _measurements = [];
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
        _listener.SetMeasurementEventCallback<long>((instrument, value, _, _) => Add(instrument.Name, value));
        _listener.SetMeasurementEventCallback<double>((instrument, value, _, _) => Add(instrument.Name, value));
        _listener.Start();
    }

    public IReadOnlyList<double> ValuesOf(string instrument)
    {
        lock (_gate)
        {
            return _measurements
                .Where(m => string.Equals(m.Key, instrument, StringComparison.Ordinal))
                .Select(m => m.Value)
                .ToList();
        }
    }

    public Instrument Published(string instrument)
    {
        lock (_gate)
            return _instruments[instrument];
    }

    public void Dispose() => _listener.Dispose();

    private void Add(string instrument, double value)
    {
        lock (_gate)
            _measurements.Add(new KeyValuePair<string, double>(instrument, value));
    }
}
