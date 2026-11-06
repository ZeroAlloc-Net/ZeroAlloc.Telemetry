; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category            | Severity | Notes
--------|---------------------|----------|-------------------------------------------------------------------------
ZTEL028 | ZeroAlloc.Telemetry | Error    | A per-element metric or a FromElement tag cannot be emitted
ZTEL027 | ZeroAlloc.Telemetry | Warning  | [Trace] and [Histogram] of one method set CancellationIsError differently
