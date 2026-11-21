; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### Changed Rules

Rule ID | New Category        | New Severity | Old Category        | Old Severity | Notes
--------|---------------------|--------------|---------------------|--------------|----------------------------------------------------------------------
ZTEL010 | ZeroAlloc.Telemetry | Error        | ZeroAlloc.Telemetry | Warning      | A [TraceTag] member path names no property or field; the tag is not emitted
ZTEL013 | ZeroAlloc.Telemetry | Error        | ZeroAlloc.Telemetry | Warning      | Instrumented interface inside a containing type that is not partial
ZTEL026 | ZeroAlloc.Telemetry | Error        | ZeroAlloc.Telemetry | Warning      | Two [TraceTag] or [TraceTagConstant] set one tag name on a span
