; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category            | Severity | Notes
--------|---------------------|----------|-------------------------------------------------------------------------
ZTEL026 | ZeroAlloc.Telemetry | Warning  | Two [TraceTag] or [TraceTagConstant] set one tag name on a span
