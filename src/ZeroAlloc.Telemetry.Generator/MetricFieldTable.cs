using System.Globalization;
using System.Text;
using ZeroAlloc.Telemetry.Generator.Models;

namespace ZeroAlloc.Telemetry.Generator;

/// <summary>
/// Assigns one static field per distinct instrument kind and metric name on a proxy. Each
/// identifier is valid C#, and distinct from every other field, member and local the proxy emits.
/// </summary>
/// <remarks>
/// <para>
/// Keying by name alone, as 1.6.4 did, dropped the histogram field whenever a counter shared its
/// name, and the proxy then referenced a field that did not exist. Deriving the identifier by
/// replacing only <c>.</c> and <c>-</c> collapsed <c>a.b</c> and <c>a_b</c> onto one field, and
/// left every other punctuation character in place, which is not a valid identifier.
/// </para>
/// <para>
/// A metric name can also collide with the proxy's own names. <c>meter</c> would redeclare
/// <c>_meter</c>, and <c>result</c> would be shadowed inside every method by the <c>_result</c>
/// local, and <c>metricTags0</c> by the tag list of a <c>[MetricTagFromResult]</c>. Those are
/// moved to a <c>_metric_</c> prefix before deduplication.
/// </para>
/// </remarks>
internal sealed class MetricFieldTable
{
    /// <summary>A field the proxy declares.</summary>
    /// <param name="Kind">Field type.</param>
    /// <param name="Metric">Metric name passed to the <c>Meter</c> factory.</param>
    /// <param name="FieldName">The full identifier, including its leading underscore.</param>
    /// <param name="Unit">The first non-null unit declared for this instrument, in <see cref="Fields"/> order.</param>
    /// <param name="Description">The first non-null description declared for this instrument, in the same order.</param>
    /// <param name="Buckets">The first non-empty bucket boundaries declared for this histogram, in the same order.</param>
    internal sealed record Field(
        MetricKind Kind, string Metric, string FieldName, string? Unit, string? Description, EquatableArray<string> Buckets);

    private readonly record struct MetricKey(MetricKind Kind, string Metric);

    // Members and locals the writer emits. A field with one of these names either fails to
    // compile or is shadowed by the local inside every method body.
    private static readonly HashSet<string> ReservedNames = new(StringComparer.Ordinal)
    {
        "_activitySource", "_meter", "_inner", "_activity", "_sw", "_result", "_tagged", "_ex", "_implName",
        "_recordEach",
    };

    private readonly Dictionary<MetricKey, int> _indexByKey;
    private readonly List<Field> _fields;

    private MetricFieldTable(List<Field> fields, Dictionary<MetricKey, int> indexByKey)
    {
        _fields = fields;
        _indexByKey = indexByKey;
    }

    /// <summary>
    /// Fields in first-use order: interface members in declaration order, and within a method
    /// <c>[Count]</c>, then <c>[Histogram]</c>, then the result metrics in attribute order. The same
    /// order decides which unit and description win, and which colliding name gets a suffix.
    /// </summary>
    public IReadOnlyList<Field> Fields => _fields;

    /// <summary>The identifier of the field that records <paramref name="metric"/>.</summary>
    public string FieldFor(MetricModel metric) =>
        _fields[_indexByKey[new MetricKey(metric.Kind, metric.Metric)]].FieldName;

    public static MetricFieldTable Build(InstrumentModel model)
    {
        var fields = new List<Field>();
        var indexByKey = new Dictionary<MetricKey, int>();
        var used = new HashSet<string>(StringComparer.Ordinal);

        foreach (var method in model.Methods)
        {
            foreach (var metric in MetricsOf(method))
            {
                var key = new MetricKey(metric.Kind, metric.Metric);
                if (indexByKey.TryGetValue(key, out var index))
                {
                    // One instrument, several declarations: the first non-null of each wins, so
                    // either can be declared once on any of them.
                    var existing = fields[index];
                    fields[index] = existing with
                    {
                        Unit = existing.Unit ?? metric.Unit,
                        Description = existing.Description ?? metric.Description,
                        Buckets = existing.Buckets.Count > 0 ? existing.Buckets : metric.Buckets,
                    };
                    continue;
                }

                indexByKey.Add(key, fields.Count);
                fields.Add(new Field(
                    metric.Kind, metric.Metric, AssignName(metric.Metric, used), metric.Unit, metric.Description, metric.Buckets));
            }
        }

        return new MetricFieldTable(fields, indexByKey);
    }

    private static IEnumerable<MetricModel> MetricsOf(MethodModel method)
    {
        if (method.Count is { } count)
            yield return count;

        if (method.Histogram is { } histogram)
            yield return histogram;

        foreach (var metric in method.ResultMetrics)
            yield return metric;
    }

    private static string AssignName(string metric, HashSet<string> used)
    {
        var candidate = "_" + Sanitize(metric);
        if (IsReserved(candidate))
            candidate = "_metric" + candidate;

        var name = candidate;
        for (var n = 2; !used.Add(name); n++)
            name = candidate + "_" + n.ToString(CultureInfo.InvariantCulture);

        return name;
    }

    /// <summary>Keeps letters, digits and underscores; every other character becomes an underscore.</summary>
    private static string Sanitize(string metric)
    {
        var sb = new StringBuilder(metric.Length);
        foreach (var c in metric)
            sb.Append(char.IsLetterOrDigit(c) || c == '_' ? c : '_');

        return sb.ToString();
    }

    private static bool IsReserved(string name) =>
        ReservedNames.Contains(name)
        || name.StartsWith("_spanName_", StringComparison.Ordinal)
        || name.StartsWith("_tag_", StringComparison.Ordinal)
        || name.StartsWith("_metricTag", StringComparison.Ordinal)
        || name.StartsWith("_core_", StringComparison.Ordinal)
        || name.StartsWith("_fault_", StringComparison.Ordinal)
        || name.StartsWith("_eachValue", StringComparison.Ordinal)
        || name.StartsWith("_nameArg", StringComparison.Ordinal)
        || name.StartsWith("_startTags", StringComparison.Ordinal)
        || IsNumberedLocal(name, "_read")
        || IsNumberedLocal(name, "_each");

    /// <summary>
    /// <c>_read0</c>, <c>_read1</c> …, the pattern locals holding a non-null metric value, and
    /// <c>_each0</c> …, the elements of a per-element histogram.
    /// </summary>
    private static bool IsNumberedLocal(string name, string prefix)
    {
        if (!name.StartsWith(prefix, StringComparison.Ordinal) || name.Length == prefix.Length)
            return false;

        for (var i = prefix.Length; i < name.Length; i++)
        {
            if (name[i] < '0' || name[i] > '9')
                return false;
        }

        return true;
    }
}
