; Unshipped analyzer release.
; https://github.com/dotnet/roslyn-analyzers/blob/main/src/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

### New Rules

Rule ID | Category            | Severity | Notes
--------|---------------------|----------|-------------------------------------------------------------------------
ZTEL018 | ZeroAlloc.Telemetry | Warning  | [Histogram] unit is not a time unit the generator converts to
ZTEL019 | ZeroAlloc.Telemetry | Error    | Histogram bucket boundaries are not finite and strictly increasing
ZTEL020 | ZeroAlloc.Telemetry | Error    | Histogram buckets need System.Diagnostics.DiagnosticSource 9.0
ZTEL021 | ZeroAlloc.Telemetry | Warning  | A constant tag value is an array or a type and records nothing
ZTEL022 | ZeroAlloc.Telemetry | Warning  | A {parameter.Member} token in a [Trace] name names no member; it is left out
ZTEL023 | ZeroAlloc.Telemetry | Warning  | Instrumentation on an accessor or a ref-returning method is ignored
ZTEL024 | ZeroAlloc.Telemetry | Warning  | A tag or name token reads a parameter value the proxy cannot read
ZTEL025 | ZeroAlloc.Telemetry | Error    | Instrumented interface has a static abstract member
