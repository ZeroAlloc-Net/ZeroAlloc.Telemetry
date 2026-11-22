using ZeroAlloc.Telemetry;

// GreeterInstrumented exists only if the compiler loaded the generator.
var proxy = new GreeterInstrumented(new Greeter());
var greeting = proxy.Greet("world");
if (greeting != "hello world")
{
    Console.Error.WriteLine($"FAIL: proxy returned '{greeting}'");
    return 1;
}

Console.WriteLine("SDK 10 consumer: generator loaded, instrumented proxy called.");
return 0;

[Instrument("Sdk10Smoke")]
public interface IGreeter
{
    [Count("greetings")]
    string Greet(string name);
}

public sealed class Greeter : IGreeter
{
    public string Greet(string name) => "hello " + name;
}
