using System;
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

Console.WriteLine("AOT smoke: PASS");
return 0;

static int Fail(string message)
{
    Console.Error.WriteLine($"AOT smoke: FAIL — {message}");
    return 1;
}
