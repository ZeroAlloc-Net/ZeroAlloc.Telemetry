using System;
using System.Diagnostics;
using System.Reflection;
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

// A result-driven counter: the value is read from the returned receipt, not a constant 1, and
// [MetricTagFromResult] adds the receipt's region as a tag.
long lines = 0;
object? region = null;
using var listener = new MeterListener();
listener.InstrumentPublished = (instrument, l) =>
{
    if (string.Equals(instrument.Meter.Name, "ZeroAlloc.Telemetry.AotSmoke", StringComparison.Ordinal)
        && string.Equals(instrument.Name, "orders.lines", StringComparison.Ordinal))
    {
        l.EnableMeasurementEvents(instrument);
    }
};
listener.SetMeasurementEventCallback<long>((_, value, tags, _) =>
{
    lines += value;
    foreach (ref readonly var tag in tags)
    {
        if (string.Equals(tag.Key, "order.region", StringComparison.Ordinal))
            region = tag.Value;
    }
});
listener.Start();

var receipt = await proxy.ReceiptAsync("cust-1", CancellationToken.None).ConfigureAwait(false);
if (receipt.Lines != 3) return Fail($"ReceiptAsync expected 3 lines, got {receipt.Lines}");
if (lines != 3) return Fail($"orders.lines expected 3, got {lines}");
if (!Equals(region, "eu-west")) return Fail($"order.region tag expected eu-west, got {region ?? "none"}");

// Metric conventions: seconds, bucket advice, a histogram per element, constant and parameter tags.
var prices = new System.Collections.Generic.List<double>();
double quoteSeconds = -1;
object? customer = null;
object? operation = null;
using var quoteListener = new MeterListener();
quoteListener.InstrumentPublished = (instrument, l) =>
{
    if (string.Equals(instrument.Meter.Name, "ZeroAlloc.Telemetry.AotSmoke", StringComparison.Ordinal)
        && instrument.Name.StartsWith("order.quote.", StringComparison.Ordinal))
    {
        l.EnableMeasurementEvents(instrument);
    }
};
quoteListener.SetMeasurementEventCallback<double>((instrument, value, tags, _) =>
{
    if (string.Equals(instrument.Name, "order.quote.prices", StringComparison.Ordinal))
        prices.Add(value);
    else
        quoteSeconds = value;

    foreach (ref readonly var tag in tags)
    {
        if (string.Equals(tag.Key, "order.customer", StringComparison.Ordinal))
            customer = tag.Value;
        else if (string.Equals(tag.Key, "order.operation", StringComparison.Ordinal))
            operation = tag.Value;
    }
});
quoteListener.Start();

await proxy.QuoteAsync("cust-7", CancellationToken.None).ConfigureAwait(false);
if (prices.Count != 2 || prices[0] != 9.5 || prices[1] != 12.25)
    return Fail($"order.quote.prices expected 9.5 and 12.25, got {prices.Count} values");
if (quoteSeconds < 0 || quoteSeconds > 1)
    return Fail($"order.quote.duration expected seconds below 1, got {quoteSeconds}");
if (!Equals(customer, "cust-7") || !Equals(operation, "quote"))
    return Fail($"quote tags expected cust-7 and quote, got {customer ?? "none"} and {operation ?? "none"}");

// Span conventions: kind, display name from a parameter, error status from the result, and the
// tags the sampler sees.
Activity? decided = null;
var samplerSawCustomer = false;
string? sourceVersion = null;
using var spanListener = new ActivityListener
{
    ShouldListenTo = source =>
    {
        if (!string.Equals(source.Name, "ZeroAlloc.Telemetry.AotSmoke", StringComparison.Ordinal))
            return false;

        sourceVersion = source.Version;
        return true;
    },
    Sample = (ref ActivityCreationOptions<ActivityContext> options) =>
    {
        if (options.Tags is { } tags)
        {
            foreach (var tag in tags)
            {
                if (string.Equals(tag.Key, "order.customer", StringComparison.Ordinal) && Equals(tag.Value, "cust-9"))
                    samplerSawCustomer = true;
            }
        }

        return ActivitySamplingResult.AllDataAndRecorded;
    },
    ActivityStopped = activity =>
    {
        if (string.Equals(activity.OperationName, "decide", StringComparison.Ordinal))
            decided = activity;
    },
};
ActivitySource.AddActivityListener(spanListener);

await proxy.DecideAsync("cust-9", CancellationToken.None).ConfigureAwait(false);
if (decided is null) return Fail("DecideAsync recorded no span");
if (decided.Kind != ActivityKind.Client) return Fail($"decide span kind expected Client, got {decided.Kind}");
if (!string.Equals(decided.DisplayName, "decide cust-9", StringComparison.Ordinal))
    return Fail($"decide span name expected 'decide cust-9', got '{decided.DisplayName}'");
if (decided.Status != ActivityStatusCode.Error || !string.Equals(decided.StatusDescription, "out of stock", StringComparison.Ordinal))
    return Fail($"decide span status expected Error 'out of stock', got {decided.Status} '{decided.StatusDescription}'");
if (!samplerSawCustomer) return Fail("the sampler did not see the order.customer tag");

// The source carries the assembly's informational version (#172).
var assemblyVersion = typeof(OrderService).Assembly
    .GetCustomAttribute<System.Reflection.AssemblyInformationalVersionAttribute>()?.InformationalVersion;
if (string.IsNullOrEmpty(sourceVersion) || !string.Equals(sourceVersion, assemblyVersion, StringComparison.Ordinal))
    return Fail($"ActivitySource version expected '{assemblyVersion}', got '{sourceVersion}'");

// Member shapes: an out parameter, a span on an awaitable method, and a property.
if (!proxy.TryFind("o-1", out var found) || found.Lines != 1) return Fail("TryFind did not forward its out parameter");
var parsed = await proxy.ParseAsync("abc".AsSpan(), out var consumed).ConfigureAwait(false);
if (parsed != 6 || consumed != 3) return Fail($"ParseAsync expected 6 and 3, got {parsed} and {consumed}");
if (proxy.Pending != 2) return Fail($"Pending expected 2, got {proxy.Pending}");

// A generic method, instantiated over a value type and a reference type.
var echoedInt = await proxy.EchoAsync(7).ConfigureAwait(false);
var echoedText = await proxy.EchoAsync("seven").ConfigureAwait(false);
if (echoedInt != 7 || !string.Equals(echoedText, "seven", StringComparison.Ordinal))
    return Fail($"EchoAsync expected 7 and seven, got {echoedInt} and {echoedText}");

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}
