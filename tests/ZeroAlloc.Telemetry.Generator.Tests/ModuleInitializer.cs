using System.Runtime.CompilerServices;
using VerifyTests;

namespace ZeroAlloc.Telemetry.Generator.Tests;

public static class ModuleInitializer
{
    [ModuleInitializer]
    public static void Init() => VerifySourceGenerators.Initialize();
}
