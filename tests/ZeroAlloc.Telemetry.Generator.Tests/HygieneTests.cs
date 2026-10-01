namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>
/// Library hygiene in the generated proxy (#172): awaits do not capture the caller's context, and
/// the source and meter carry a version.
/// </summary>
public class HygieneTests
{
    [Fact]
    public void EveryAwait_IsConfigureAwaitFalse()
    {
        var run = GeneratorCompilation.RunAll("""
            using System.Threading.Tasks;
            using ZeroAlloc.Telemetry;
            [Instrument("a")]
            public interface IOps
            {
                [Trace("t")] Task RunAsync();
                [Count("c")] Task<int> CountAsync();
                [Histogram("h")] ValueTask StepAsync();
                [Trace("v")] ValueTask<int> ReadAsync();
            }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        var proxy = Proxy(run, "IOps");
        proxy.Should().Contain("await _inner.RunAsync().ConfigureAwait(false);");
        proxy.Should().Contain("var _result = await _inner.CountAsync().ConfigureAwait(false);");
        proxy.Should().Contain("await _inner.StepAsync().ConfigureAwait(false);");
        proxy.Should().Contain("var _result = await _inner.ReadAsync().ConfigureAwait(false);");
        proxy.Should().Contain("await global::System.Threading.Tasks.Task.CompletedTask.ConfigureAwait(false);");
        CountOccurrences(proxy, "await ").Should().Be(CountOccurrences(proxy, ".ConfigureAwait(false)"));
    }

    [Fact]
    public void Version_DefaultsToTheAssemblysInformationalVersion()
    {
        var run = GeneratorCompilation.RunAll("""
            using System.Reflection;
            using ZeroAlloc.Telemetry;
            [assembly: AssemblyInformationalVersion("2.3.4+abc123")]
            [Instrument("a")] public interface IDefault { [Trace("t")] void Run(); }
            [Instrument("b", Version = "9.0.0")] public interface IExplicit { [Trace("t")] void Run(); }
            [Instrument("c", Version = "")] public interface INone { [Trace("t")] void Run(); }
            """);

        run.GeneratorDiagnostics.Should().BeEmpty();
        run.Errors.Should().BeEmpty();
        Proxy(run, "IDefault").Should().Contain("_activitySource = new(\"a\", \"2.3.4+abc123\");")
            .And.Contain("_meter = new(\"a\", \"2.3.4+abc123\");");
        Proxy(run, "IExplicit").Should().Contain("_activitySource = new(\"b\", \"9.0.0\");")
            .And.Contain("_meter = new(\"b\", \"9.0.0\");");
        Proxy(run, "INone").Should().Contain("_activitySource = new(\"c\");").And.Contain("_meter = new(\"c\");");
    }

    [Fact]
    public void Version_WithoutAnInformationalVersion_IsLeftOut()
    {
        var run = GeneratorCompilation.RunAll("""
            using ZeroAlloc.Telemetry;
            [Instrument("a")] public interface IOps { void Run(); }
            """);

        Proxy(run, "IOps").Should().Contain("_activitySource = new(\"a\");").And.Contain("_meter = new(\"a\");");
    }

    private static string Proxy(GeneratorOutput run, string interfaceName) =>
        run.Output.SyntaxTrees
            .First(t => t.FilePath.EndsWith(interfaceName + ".Instrumented.g.cs", StringComparison.Ordinal))
            .ToString();

    private static int CountOccurrences(string text, string value)
    {
        var count = 0;
        for (var i = text.IndexOf(value, StringComparison.Ordinal); i >= 0; i = text.IndexOf(value, i + value.Length, StringComparison.Ordinal))
            count++;
        return count;
    }
}
