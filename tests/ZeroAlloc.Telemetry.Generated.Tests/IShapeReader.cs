namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>A base interface: the proxy of an interface extending it implements its members too (#173).</summary>
public interface IShapeReader
{
    [Trace("shape.peek")]
    int Peek();
}
