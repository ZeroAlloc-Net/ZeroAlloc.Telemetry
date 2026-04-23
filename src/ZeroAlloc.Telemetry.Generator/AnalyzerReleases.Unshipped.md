; Unshipped analyzer release
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category | Severity | Notes
--------|----------|----------|-------
ZTEL001 | ZeroAlloc.Telemetry | Error | [Instrument] cannot be applied to a non-interface type
ZTEL002 | ZeroAlloc.Telemetry | Error | [Instrument] requires a non-empty ActivitySource name
ZTEL003 | ZeroAlloc.Telemetry | Warning | [Trace]/[Count]/[Histogram] on a method whose containing type lacks [Instrument] is ignored
