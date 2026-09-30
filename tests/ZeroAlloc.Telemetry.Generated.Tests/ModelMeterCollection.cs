namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>
/// Serialises the tests that use the <see cref="IModelService"/> meter. A listener attached by
/// one of them enables its instruments for all of them, which would break the allocation test.
/// </summary>
[CollectionDefinition(Name)]
public sealed class ModelMeterCollection
{
    public const string Name = "Model meter";

    public const string MeterName = "ZeroAlloc.Telemetry.Generated.Tests.Model";
}
