using Microsoft.CodeAnalysis;

namespace ZeroAlloc.Telemetry.Generator;

internal static class InstrumentDiagnostics
{
    private const string Category = "ZeroAlloc.Telemetry";

    public static readonly DiagnosticDescriptor InstrumentOnNonInterface = new(
        id: "ZTEL001",
        title: "[Instrument] only applies to interfaces",
        messageFormat: "[Instrument] cannot be applied to '{0}' — the generator emits a proxy class that implements the target, which only makes sense on an interface. Apply [Instrument] to an interface instead.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor EmptyActivitySource = new(
        id: "ZTEL002",
        title: "[Instrument] requires a non-empty ActivitySource name",
        messageFormat: "[Instrument] on '{0}' has an empty ActivitySource name. The generated proxy will create an ActivitySource with no name, which makes tracing subscriptions effectively unroutable. Supply a meaningful name — typically the fully-qualified service or module name.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MethodAttributeWithoutInstrument = new(
        id: "ZTEL003",
        title: "Telemetry attribute on a method in a type without [Instrument] is ignored",
        messageFormat: "[{0}] on '{1}.{2}' is ignored — the containing type does not have [Instrument] applied, so no proxy is generated. Either apply [Instrument] to the enclosing interface or remove this attribute.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor TagWithoutTrace = new(
        id: "ZTEL004",
        title: "[TraceTag]/[TraceTagFromResult] without [Trace] records nothing",
        messageFormat: "[{0}] on '{1}.{2}' records nothing — the method has no [Trace], so no span is started to carry the tag. Add [Trace] to the method or remove the tag.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor ResultReadOnVoidMethod = new(
        id: "ZTEL005",
        title: "Result-reading attribute on a method with no return value",
        messageFormat: "[{0}] on '{1}.{2}' records nothing — the method returns void, Task or ValueTask, so there is no result to read. Remove it or return a value.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor UnknownSpanNameToken = new(
        id: "ZTEL006",
        title: "Unrecognised token in a [Trace] span name",
        messageFormat: "'{0}' in the [Trace] name on '{1}.{2}' is not a recognised token and is emitted verbatim, so the span name will contain a literal brace. The supported tokens are {{type}}, which substitutes the wrapped implementation's type name, and {{parameter}} or {{parameter.Member}} for a parameter of the method.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberPathNotFound = new(
        id: "ZTEL007",
        title: "Member path does not resolve",
        messageFormat: "'{0}' in the path '{1}' is not a readable instance property or field of '{2}'. Each segment of a member path names a property or field of the type reached so far, starting from the awaited return value.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// ZTEL007 for a path such as <c>Total..Count</c>. Same rule, but there is no segment name to
    /// quote, and a quoted empty name reads as a generator bug rather than a typo.
    /// </summary>
    public static readonly DiagnosticDescriptor MemberPathEmptySegment = new(
        id: "ZTEL007",
        title: "Member path does not resolve",
        messageFormat: "The path '{0}' has an empty segment. Each segment of a member path names a property or field of the type reached so far, starting from the awaited return value.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor WhenNotBoolean = new(
        id: "ZTEL008",
        title: "When must name a bool member",
        messageFormat: "{2} = '{0}' resolves to '{1}'. A guard must name a member of type bool or bool? on the awaited return value, such as IsSuccess.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public static readonly DiagnosticDescriptor MemberDoesNotFitInstrument = new(
        id: "ZTEL009",
        title: "Member type does not fit the instrument",
        messageFormat: "[{0}] cannot record {1} of type '{2}'. {3}.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// The <c>[TraceTag]</c> counterpart of ZTEL007. A warning, not an error: before this rule a
    /// bad parameter path compiled and tagged the whole argument, so an error would break builds
    /// that work today. The tag is not emitted, so this warning is the only sign of the typo.
    /// </summary>
    public static readonly DiagnosticDescriptor ParameterTagPathNotFound = new(
        id: "ZTEL010",
        title: "Parameter tag member path does not resolve",
        messageFormat: "'{0}' in the path '{1}' is not a readable instance property or field of '{2}', so [{4}] on '{3}' records nothing. Each segment of a member path names a property or field of the type reached so far, starting from the parameter.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>ZTEL010 for a path with an empty segment; see <see cref="MemberPathEmptySegment"/>.</summary>
    public static readonly DiagnosticDescriptor ParameterTagPathEmptySegment = new(
        id: "ZTEL010",
        title: "Parameter tag member path does not resolve",
        messageFormat: "The path '{0}' has an empty segment, so [{2}] on '{1}' records nothing. Each segment of a member path names a property or field of the type reached so far, starting from the parameter.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// A <c>[MetricTagFromResult]</c> whose <c>Metric</c> names no metric the method declares. A
    /// warning, like the other rules for an attribute that records nothing: ZTEL004 and ZTEL005.
    /// </summary>
    public static readonly DiagnosticDescriptor MetricTagUnknownMetric = new(
        id: "ZTEL011",
        title: "Metric tag is added to no metric",
        messageFormat: "[{4}({0})] on '{1}.{2}' records nothing — Metric = '{3}' names no metric the method records. Name one of its [Count], [Histogram], [CountFromResult] or [HistogramFromResult] metrics, or remove Metric to tag them all.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>ZTEL011 for a method that declares no metric at all.</summary>
    public static readonly DiagnosticDescriptor MetricTagWithoutMetric = new(
        id: "ZTEL011",
        title: "Metric tag is added to no metric",
        messageFormat: "[{3}({0})] on '{1}.{2}' records nothing — the method records no metric to carry the tag. Add [Count], [Histogram], [CountFromResult] or [HistogramFromResult], or remove the tag.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Two <c>[MetricTagFromResult]</c> with one tag name on one metric. An error: the measurement
    /// could carry only one of the values, and which one would be up to the exporter.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateMetricTag = new(
        id: "ZTEL012",
        title: "Duplicate metric tag name",
        messageFormat: "[{4}] adds the tag '{0}' to '{1}', which another metric tag on '{2}.{3}' already adds. A measurement carries one value per tag name, so remove or rename one of them.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// A nested interface's proxy is emitted inside partial declarations of its containing types,
    /// so each of them must be partial.
    /// </summary>
    public static readonly DiagnosticDescriptor ContainingTypeNotPartial = new(
        id: "ZTEL013",
        title: "Instrumented interface inside a containing type that is not partial",
        messageFormat: "Instrumented interface '{0}' gets no proxy because its containing type '{1}' is not partial",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>A file-local interface is visible only in its own file, not in the proxy's.</summary>
    public static readonly DiagnosticDescriptor FileLocal = new(
        id: "ZTEL014",
        title: "File-local instrumented interface",
        messageFormat: "Instrumented interface '{0}' gets no proxy because '{1}' is file-local",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Roslyn compares hint names ignoring case, so interfaces whose names differ only in case
    /// would need the same file.
    /// </summary>
    public static readonly DiagnosticDescriptor NameDiffersOnlyInCase = new(
        id: "ZTEL015",
        title: "Instrumented interface name differs only in case from another",
        messageFormat: "Instrumented interface '{0}' gets no proxy because its file name '{1}' differs only in case from that of '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// The proxy is named after the interface without a leading I, so <c>IFoo</c> and <c>Foo</c>
    /// would both get <c>FooInstrumented</c>.
    /// </summary>
    public static readonly DiagnosticDescriptor ProxyNameCollision = new(
        id: "ZTEL016",
        title: "Instrumented interfaces share a proxy name",
        messageFormat: "Instrumented interface '{0}' gets no proxy because its proxy '{1}' has the same name as the proxy of '{2}'",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// C# does not allow a class inside an interface with an in or out type parameter, CS8427, so
    /// the proxy cannot be emitted next to an interface nested in one.
    /// </summary>
    public static readonly DiagnosticDescriptor VariantContainingInterface = new(
        id: "ZTEL017",
        title: "Instrumented interface inside a variant interface",
        messageFormat: "Instrumented interface '{0}' gets no proxy because its containing interface '{1}' has a variant type parameter, and a class cannot be declared in it",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// A <c>[Histogram]</c> unit that is not a time unit the generator can convert the elapsed time
    /// to. A warning: the duration is still recorded, in milliseconds, as before units were honoured.
    /// </summary>
    public static readonly DiagnosticDescriptor UnconvertibleHistogramUnit = new(
        id: "ZTEL018",
        title: "[Histogram] unit is not a time unit",
        messageFormat: "[Histogram(\"{0}\")] has Unit = '{1}', which is not a time unit the generator converts to, so durations are recorded in milliseconds under the unit '{1}'. Use ms, s, us, ns, min or h.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// Bucket boundaries that the runtime would reject. <c>InstrumentAdvice</c> throws for them
    /// when the proxy's static fields are initialised, which would fail every use of the proxy.
    /// </summary>
    public static readonly DiagnosticDescriptor InvalidHistogramBuckets = new(
        id: "ZTEL019",
        title: "Histogram bucket boundaries are invalid",
        messageFormat: "Buckets on [{0}(\"{1}\")] {2}. Bucket boundaries must be finite and in strictly increasing order.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Buckets need <c>InstrumentAdvice&lt;T&gt;</c>, which System.Diagnostics.DiagnosticSource added
    /// in 9.0. The instrument is created without advice.
    /// </summary>
    public static readonly DiagnosticDescriptor HistogramBucketsUnavailable = new(
        id: "ZTEL020",
        title: "Histogram buckets need System.Diagnostics.DiagnosticSource 9.0",
        messageFormat: "Buckets on [{0}(\"{1}\")] need System.Diagnostics.Metrics.InstrumentAdvice<T>, which this compilation does not have. Target .NET 9 or later, or reference the System.Diagnostics.DiagnosticSource package 9.0 or later.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// A constant tag whose value is an array or a type, which no tag can carry. Before this rule
    /// such a <c>[TraceTagConstant]</c> was dropped without a word.
    /// </summary>
    public static readonly DiagnosticDescriptor UnsupportedConstantTagValue = new(
        id: "ZTEL021",
        title: "Constant tag value cannot be recorded",
        messageFormat: "[{0}(\"{1}\", ...)] on '{2}.{3}' records nothing — its value is an array or a type. A constant tag value must be a string, bool, char, number or enum.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// A <c>{parameter.Member}</c> token in a span name whose path does not resolve. A warning,
    /// like ZTEL010: the token is left out of the name, which still identifies the operation.
    /// </summary>
    public static readonly DiagnosticDescriptor SpanNameTokenPathNotFound = new(
        id: "ZTEL022",
        title: "Span name token path does not resolve",
        messageFormat: "'{0}' in the token '{1}' of the [Trace] name on '{2}.{3}' is not a readable instance property or field of '{4}', so the token is left out of the span name",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// An instrumentation attribute on a member the proxy forwards but cannot instrument: an
    /// accessor of a property, indexer or event, or a method returning by reference. The member
    /// still compiles and is forwarded, so this is a warning.
    /// </summary>
    public static readonly DiagnosticDescriptor UninstrumentableMember = new(
        id: "ZTEL023",
        title: "Instrumentation is not supported on this member",
        messageFormat: "[{0}] on '{1}.{2}' records nothing — {3}. The member is forwarded without instrumentation.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// A tag or name token reading a parameter whose value the proxy cannot read where it would
    /// need it. The tag or token is left out.
    /// </summary>
    public static readonly DiagnosticDescriptor UnreadableParameter = new(
        id: "ZTEL024",
        title: "Parameter value cannot be recorded",
        messageFormat: "[{0}] on parameter '{1}' of '{2}.{3}' records nothing — {4}",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);

    /// <summary>
    /// A static abstract member: the proxy has to implement it as a static member, which cannot
    /// reach the wrapped instance.
    /// </summary>
    public static readonly DiagnosticDescriptor StaticAbstractMember = new(
        id: "ZTEL025",
        title: "Instrumented interface has a static abstract member",
        messageFormat: "Instrumented interface '{0}' gets no proxy because its member '{1}' is static abstract, and a proxy cannot forward a static member to the instance it wraps",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    /// <summary>
    /// Two span tags with one name, from <c>[TraceTag]</c> or <c>[TraceTagConstant]</c>. A warning,
    /// not an error like ZTEL012: a span keeps the last value set, so such code ran before, but the
    /// later tag silently replaced the earlier one.
    /// </summary>
    public static readonly DiagnosticDescriptor DuplicateSpanTag = new(
        id: "ZTEL026",
        title: "Duplicate span tag name",
        messageFormat: "[{0}] adds the tag '{1}' to the span of '{2}.{3}', which another tag already sets. A span keeps one value per tag name, so remove or rename one of them; this one is not set.",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Warning,
        isEnabledByDefault: true);
}
