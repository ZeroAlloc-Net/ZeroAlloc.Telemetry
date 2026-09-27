using System;
using System.Diagnostics.Metrics;
using System.Threading;
using System.Threading.Tasks;
using ZeroAlloc.Telemetry.AotSmoke;

// Exercise the generator-emitted OrderServiceInstrumented proxy (strips
// leading 'I', appends 'Instrumented') under PublishAot=true. The proxy
// wraps every method in Activity + Meter calls — this verifies that whole
// chain compiles and runs AOT-safely.

var impl = new OrderService();
var proxy = new OrderServiceInstrumented(impl);

var id = await proxy.CreateAsync("cust-1", CancellationToken.None).ConfigureAwait(false);
if (id != 42) return Fail($"CreateAsync expected 42, got {id}");
if (impl.CallCount != 1) return Fail($"Inner call count expected 1, got {impl.CallCount}");

// Multiple invocations — each should reach the inner
for (var i = 0; i < 3; i++)
{
    _ = await proxy.CreateAsync("cust", CancellationToken.None).ConfigureAwait(false);
}
if (impl.CallCount != 4) return Fail($"After 4 total invocations, CallCount expected 4, got {impl.CallCount}");

// A result-driven counter: the value is read from the returned receipt, not a constant 1.
long lines = 0;
using var listener = new MeterListener();
listener.InstrumentPublished = (instrument, l) =>
{
    if (string.Equals(instrument.Meter.Name, "ZeroAlloc.Telemetry.AotSmoke", StringComparison.Ordinal)
        && string.Equals(instrument.Name, "orders.lines", StringComparison.Ordinal))
    {
        l.EnableMeasurementEvents(instrument);
    }
};
listener.SetMeasurementEventCallback<long>((_, value, _, _) => lines += value);
listener.Start();

var receipt = await proxy.ReceiptAsync("cust-1", CancellationToken.None).ConfigureAwait(false);
if (receipt.Lines != 3) return Fail($"ReceiptAsync expected 3 lines, got {receipt.Lines}");
if (lines != 3) return Fail($"orders.lines expected 3, got {lines}");

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}
