; Shipped analyzer releases.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.1.0

### New Rules

Rule ID | Category            | Severity | Notes
--------|---------------------|----------|---------------------------------------------------------------------------------------------
ZTEL001 | ZeroAlloc.Telemetry | Error    | [Instrument] cannot be applied to a non-interface type
ZTEL002 | ZeroAlloc.Telemetry | Error    | [Instrument] requires a non-empty ActivitySource name
ZTEL003 | ZeroAlloc.Telemetry | Warning  | [Trace]/[Count]/[Histogram] on a method whose containing type lacks [Instrument] is ignored

## Release 1.4.0

### New Rules

Rule ID | Category            | Severity | Notes
--------|---------------------|----------|-----------------------------------------------------------------------------
ZTEL004 | ZeroAlloc.Telemetry | Warning  | [TraceTag]/[TraceTagFromResult] on a method without [Trace] records nothing
ZTEL005 | ZeroAlloc.Telemetry | Warning  | [TraceTagFromResult] on a method with no return value records nothing

## Release 1.6.0

### New Rules

Rule ID | Category            | Severity | Notes
--------|---------------------|----------|-----------------------------------------------------------------
ZTEL006 | ZeroAlloc.Telemetry | Warning  | Unrecognised {token} in a [Trace] span name is emitted verbatim
