using System.Globalization;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.ClickHouse;

// The exporter's rows as K3's normalised records: attributes are strings with the resource's merged in under the record's own,
// times are UTC at microsecond precision, kind and status are the contract's enums, and the severity is the OTel text in upper case.
public static class ClickHouseRowMapping
{
    // The columns SpanSelect reads, in order. The attribute map is the resource's with the span's laid over it.
    public const string SpanSelect = "TraceId, SpanId, ParentSpanId, SpanName, SpanKind, Timestamp, Duration, StatusCode, StatusMessage, "
        + "mapUpdate(ResourceAttributes, SpanAttributes), `Events.Timestamp`, `Events.Name`, `Events.Attributes`";

    // The columns LogSelect reads, in order.
    public const string LogSelect = "Timestamp, SeverityText, SeverityNumber, Body, mapUpdate(ResourceAttributes, LogAttributes), TraceId, SpanId, EventName";

    public static SpanRecord Span(object?[] row)
    {
        var start = Time(row[5]!);
        var eventTimes = (DateTime[])row[10]!;
        var eventNames = (string[])row[11]!;
        var eventAttributes = (object[])row[12]!;
        var events = new List<SpanEventData>(eventNames.Length);
        for (var i = 0; i < eventNames.Length; i++)
        {
            events.Add(new SpanEventData(Time(eventTimes[i]), eventNames[i], Attributes(eventAttributes[i])));
        }
        return new SpanRecord(
            (string)row[0]!,
            (string)row[1]!,
            Empty((string)row[2]!),
            (string)row[3]!,
            Kind((string)row[4]!),
            start,
            start.AddTicks(Convert.ToInt64(row[6], CultureInfo.InvariantCulture) / 1000 * 10),
            Status((string)row[7]!),
            Empty((string)row[8]!),
            Attributes(row[9]),
            events);
    }

    public static LogRecordData Log(object?[] row, InspectionFieldNames fields)
    {
        var attributes = Attributes(row[4]);
        var severity = (string)row[1]!;
        return new LogRecordData(
            Time(row[0]!),
            severity.Length > 0 ? severity.ToUpperInvariant() : Severity(Convert.ToInt32(row[2], CultureInfo.InvariantCulture)),
            (string)row[3]!,
            attributes,
            Empty((string)row[5]!),
            Empty((string)row[6]!),
            attributes.GetValueOrDefault(fields.Session),
            attributes.GetValueOrDefault(fields.Prompt),
            Empty((string)row[7]!) ?? attributes.GetValueOrDefault("event.name"));
    }

    // The exporter writes the span kind as "Internal", "Client", ...; the contract says lower case.
    private static string Kind(string kind) => kind.ToLowerInvariant() switch
    {
        "server" or "client" or "producer" or "consumer" => kind.ToLowerInvariant(),
        _ => "internal",
    };

    private static SpanStatus Status(string code) => code.ToLowerInvariant() switch
    {
        "ok" => SpanStatus.Ok,
        "error" => SpanStatus.Error,
        _ => SpanStatus.Unset,
    };

    // OTel severity numbers: 1-4 TRACE, 5-8 DEBUG, 9-12 INFO, 13-16 WARN, 17-20 ERROR, 21-24 FATAL.
    private static string Severity(int number) => number switch
    {
        >= 21 => "FATAL",
        >= 17 => "ERROR",
        >= 13 => "WARN",
        >= 9 => "INFO",
        >= 5 => "DEBUG",
        _ => "TRACE",
    };

    private static string? Empty(string value) => value.Length == 0 ? null : value;

    private static Dictionary<string, string> Attributes(object? map)
        => ((IDictionary<string, string>)map!).ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);

    // The driver gives DateTime64(9) at 100 ns; the contract is microseconds.
    private static DateTimeOffset Time(object value)
    {
        var time = DateTime.SpecifyKind((DateTime)value, DateTimeKind.Utc);
        return new DateTimeOffset(time.AddTicks(-(time.Ticks % 10)));
    }
}
