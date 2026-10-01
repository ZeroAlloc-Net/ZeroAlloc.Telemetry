using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator.Tests;

/// <summary>The references a consumer compiles with: the running platform and the attributes.</summary>
internal static class GeneratorTestReferences
{
    public static MetadataReference[] All()
    {
        var trustedPlatformAssemblies = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? string.Empty;
        return trustedPlatformAssemblies
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Select(p => (MetadataReference)MetadataReference.CreateFromFile(p))
            .Append(MetadataReference.CreateFromFile(typeof(InstrumentAttribute).Assembly.Location))
            .ToArray();
    }
}
