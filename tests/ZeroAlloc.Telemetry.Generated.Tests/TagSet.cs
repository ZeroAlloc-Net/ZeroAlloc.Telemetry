namespace ZeroAlloc.Telemetry.Generated.Tests;

/// <summary>A set of itself, so it satisfies <c>T : ISet&lt;T&gt;</c>.</summary>
internal sealed class TagSet : HashSet<TagSet>
{
    public void Add(string name) => Add(new TagSet { Name = name });

    public string? Name { get; init; }
}
