; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category            | Severity | Notes
--------|---------------------|----------|----------------------------------------------------------
ZTEL011 | ZeroAlloc.Telemetry | Warning  | [MetricTagFromResult] names no metric the method records
ZTEL012 | ZeroAlloc.Telemetry | Error    | Two [MetricTagFromResult] add one tag name to one metric
