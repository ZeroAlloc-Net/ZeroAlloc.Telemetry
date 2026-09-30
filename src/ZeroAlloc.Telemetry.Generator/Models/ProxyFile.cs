namespace ZeroAlloc.Telemetry.Generator.Models;

/// <summary>
/// A proxy the generator would add a file for, as the collision check sees it.
/// </summary>
/// <param name="HintName">The file name, which Roslyn compares ignoring case.</param>
/// <param name="ProxyKey">
/// The proxy's metadata name, such as <c>App.Outer+FooInstrumented`1</c>, which C# compares
/// ordinally.
/// </param>
/// <param name="ProxyDisplayName">The proxy as the diagnostic names it.</param>
/// <param name="InterfaceDisplayName">The interface as the diagnostic names it.</param>
/// <param name="Location">The interface's name, where the diagnostic is reported.</param>
internal sealed record ProxyFile(
    string HintName,
    string ProxyKey,
    string ProxyDisplayName,
    string InterfaceDisplayName,
    LocationInfo Location);
