; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category            | Severity | Notes
--------|---------------------|----------|---------------------------------------------------------------------------
ZTEL007 | ZeroAlloc.Telemetry | Error    | A segment of a member path or When guard names no property or field
ZTEL008 | ZeroAlloc.Telemetry | Error    | A When guard resolves to a member that is not bool or bool?
ZTEL009 | ZeroAlloc.Telemetry | Error    | A result-driven metric's member does not fit its instrument
