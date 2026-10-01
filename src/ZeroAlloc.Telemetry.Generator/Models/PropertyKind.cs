namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>What kind of non-method member a <see cref="PropertyModel"/> forwards.</summary>
internal enum PropertyKind
{
    Property,
    Indexer,
    Event,
}
