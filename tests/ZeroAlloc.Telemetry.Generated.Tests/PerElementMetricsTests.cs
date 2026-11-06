using System.Collections.Immutable;
using ZeroAlloc.TestHelpers;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// One measurement per element of a collection of records, with the value and tags read from that
/// element (#241): each backend call is tagged with its own model, not the last one.
/// </summary>
/// <remarks>The class is the only user of its meter, so its tests run sequentially and in isolation.</remarks>
public sealed class PerElementMetricsTests
{
    private const string Usage = "elem.token.usage";
    private const string Output = "elem.tokens.output";

    private static PerElementClientInstrumented Proxy(params BackendCall?[] calls) =>
        new(new PerElementClient(new PerElementResponse([.. calls])));

    [Fact]
    public void EachElement_IsOneMeasurement_TaggedWithItsOwnModel()
    {
        using var capture = new MetricCapture(PerElementClient.MeterName);

        Proxy(new BackendCall("model-a", 10, 1), new BackendCall("model-b", 20, 2)).Evaluate();

        capture.ValuesOf(Usage).Should().Equal(10d, 20d);
        var tags = capture.TagsOf(Usage);
        tags.Should().HaveCount(2);
        tags[0].Should().Equal(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["elem.token.type"] = "input", ["elem.result.size"] = 2, ["elem.response.model"] = "model-a",
        });
        tags[1].Should().Equal(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["elem.token.type"] = "input", ["elem.result.size"] = 2, ["elem.response.model"] = "model-b",
        });
    }

    [Fact]
    public void ACounter_AddsEachElementsValue_WithoutTheElementTagOfAnotherMetric()
    {
        using var capture = new MetricCapture(PerElementClient.MeterName);

        Proxy(new BackendCall("model-a", 10, 1), new BackendCall("model-b", 20, 2)).Evaluate();

        capture.ValuesOf(Output).Should().Equal(1d, 2d);
        // The element tag names elem.token.usage only, so the counter carries the result tag alone.
        capture.TagsOf(Output).Should().AllSatisfy(tags => tags.Should().Equal(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["elem.result.size"] = 2 }));
    }

    [Fact]
    public void ANullElement_ANullValue_AndANullTagValue_AreSkippedOrLeftOut()
    {
        using var capture = new MetricCapture(PerElementClient.MeterName);

        Proxy(null, new BackendCall("model-a", null, 5), new BackendCall(null, 7, null)).Evaluate();

        capture.ValuesOf(Usage).Should().Equal(7d);
        // A null model adds no tag; the measurement is still recorded.
        capture.TagsOf(Usage).Should().ContainSingle().Which.Should().Equal(
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["elem.token.type"] = "input", ["elem.result.size"] = 3 });
        capture.ValuesOf(Output).Should().Equal(5d);
    }

    [Fact]
    public void AnEmptyCollection_RecordsNothing()
    {
        using var capture = new MetricCapture(PerElementClient.MeterName);

        Proxy().Evaluate();

        capture.ValuesOf(Usage).Should().BeEmpty();
        capture.ValuesOf(Output).Should().BeEmpty();
    }

    [Fact]
    public void WithNoListener_TheCallAllocatesNothing_AndNoElementIsRead()
    {
        var proxy = Proxy(new BackendCall("model-a", 10, 1), new BackendCall("model-b", 20, 2));

        AllocationGate.AssertBudget(0, 1_000, () => proxy.Evaluate(), "PerElementClientInstrumented.Evaluate with no listener");
    }
}
