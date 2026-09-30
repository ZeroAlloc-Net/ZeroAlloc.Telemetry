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

## Release 1.7.0

### New Rules

Rule ID | Category            | Severity | Notes
--------|---------------------|----------|---------------------------------------------------------------------------
ZTEL007 | ZeroAlloc.Telemetry | Error    | A segment of a member path or When guard names no property or field
ZTEL008 | ZeroAlloc.Telemetry | Error    | A When guard resolves to a member that is not bool or bool?
ZTEL009 | ZeroAlloc.Telemetry | Error    | A result-driven metric's member does not fit its instrument
ZTEL010 | ZeroAlloc.Telemetry | Warning  | A [TraceTag] member path names no property or field; the tag is not emitted

## Release 1.8.0

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
