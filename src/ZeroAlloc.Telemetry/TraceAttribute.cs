namespace ZeroAlloc.Telemetry;

/// <summary>
/// Wraps the method body in a <see cref="System.Diagnostics.Activity"/> span.
/// The span is started before the call, stopped in a <c>finally</c>, and marked
/// <see cref="System.Diagnostics.ActivityStatusCode.Error"/> on exception.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class TraceAttribute : Attribute
{
    /// <summary>The operation name passed to <see cref="System.Diagnostics.ActivitySource.StartActivity(string)"/>.</summary>
    public string Name { get; }

    public TraceAttribute(string name) => Name = name;
}
