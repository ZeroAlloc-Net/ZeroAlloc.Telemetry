using System;

namespace ZeroAlloc.Telemetry.AotSmoke;

public readonly struct OrderQuote
{
    public ReadOnlyMemory<double> Prices { get; init; }
}
