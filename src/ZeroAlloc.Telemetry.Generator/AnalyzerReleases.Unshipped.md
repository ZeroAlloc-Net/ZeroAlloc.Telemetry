; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category            | Severity | Notes
--------|---------------------|----------|---------------------------------------------------------------------
ZTEL011 | ZeroAlloc.Telemetry | Warning  | [MetricTagFromResult] names no metric the method records
ZTEL012 | ZeroAlloc.Telemetry | Error    | Two [MetricTagFromResult] add one tag name to one metric
ZTEL013 | ZeroAlloc.Telemetry | Warning  | Instrumented interface inside a containing type that is not partial
ZTEL014 | ZeroAlloc.Telemetry | Error    | File-local instrumented interface
ZTEL015 | ZeroAlloc.Telemetry | Error    | Instrumented interface name differs only in case from another
ZTEL016 | ZeroAlloc.Telemetry | Error    | Instrumented interfaces share a proxy name
ZTEL017 | ZeroAlloc.Telemetry | Error    | Instrumented interface inside a variant interface
