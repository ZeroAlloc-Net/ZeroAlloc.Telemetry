using System.Diagnostics;

namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// The ZeroAlloc.Jev shape, built by the real generator in this project: an internal interface
/// whose generic methods return <c>ValueTask&lt;Result&lt;T, E&gt;&gt;</c> with a self-referencing
/// <c>ISet&lt;T&gt;</c> constraint (#168). Before the fix its proxy did not compile.
/// </summary>
public sealed class JevOperationsTests : IDisposable
{
    private const string SourceName = "ZeroAlloc.Jev";

    private readonly MetricCapture _capture = new(SourceName);
    private readonly ActivityListener _listener;
    private readonly List<Activity> _spans = [];
    private readonly Lock _gate = new();

    public JevOperationsTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = s => string.Equals(s.Name, SourceName, StringComparison.Ordinal),
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStopped = a =>
            {
                lock (_gate)
                    _spans.Add(a);
            },
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose()
    {
        _listener.Dispose();
        _capture.Dispose();
    }

    [Fact]
    public async Task GenericMethods_ForwardCalls_AndRecordTheirSpansAndMetrics()
    {
        IJevOperations proxy = new JevOperationsInstrumented(new JevOperations());

        var union = await proxy.UnionAsync(new TagSet { "a" }, new TagSet { "b" }).ConfigureAwait(true);
        var evaluated = await proxy.EvaluateAsync<TagSet, string>(new TagSet()).ConfigureAwait(true);

        union.IsFailure.Should().BeFalse();
        union.Value.Should().HaveCount(2);
        evaluated.IsFailure.Should().BeTrue();
        evaluated.Error.Should().Be("empty");

        lock (_gate)
            _spans.Select(s => s.OperationName).Should().Equal("jev.union", "jev.evaluate");
        _capture.ValuesOf("jev.operations").Should().Equal(1d);
        _capture.ValuesOf("jev.duration").Should().ContainSingle();
    }

    private sealed class JevOperations : IJevOperations
    {
        public async ValueTask<Result<T, string>> UnionAsync<T>(T left, T right) where T : ISet<T>, new()
        {
            await Task.Yield();
            var union = new T();
            union.UnionWith(left);
            union.UnionWith(right);
            return Result<T, string>.Success(union);
        }

        public async ValueTask<Result<T, E>> EvaluateAsync<T, E>(T input) where T : ISet<T> where E : notnull
        {
            await Task.Yield();
            return input.Count == 0
                ? Result<T, E>.Failure((E)(object)"empty")
                : Result<T, E>.Success(input);
        }
    }
}
