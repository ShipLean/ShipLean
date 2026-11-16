using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShipLean.TestSupport.Inspection;

// K3 attribute normalisation applied to OTLP/JSON: resource attributes merged into the record's, the record's own value winning on a
// clash; ints and doubles in invariant culture, bools true|false, arrays as compact JSON.
public static class OtlpAttributes
{
    public static IReadOnlyDictionary<string, string> Normalise(JsonArray? resource, JsonArray? record)
    {
        var merged = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var attributes in new[] { resource, record })
        {
            foreach (var attribute in attributes?.OfType<JsonObject>() ?? [])
            {
                merged[attribute["key"]!.GetValue<string>()] = Text(attribute["value"]!.AsObject());
            }
        }
        return merged;
    }

    private static string Text(JsonObject value)
    {
        var (kind, node) = value.Single();
        return kind switch
        {
            "stringValue" => node!.GetValue<string>(),
            "intValue" => long.Parse(node!.ToString(), CultureInfo.InvariantCulture).ToString(CultureInfo.InvariantCulture),
            "doubleValue" => node!.GetValue<double>().ToString("R", CultureInfo.InvariantCulture),
            "boolValue" => node!.GetValue<bool>() ? "true" : "false",
            "arrayValue" => new JsonArray([.. node!["values"]!.AsArray().Select(v => Plain(v!.AsObject()))]).ToJsonString(),
            _ => throw new NotSupportedException($"OTLP value kind {kind}"),
        };
    }

    private static JsonNode? Plain(JsonObject value)
    {
        var (kind, node) = value.Single();
        return kind switch
        {
            "intValue" => JsonValue.Create(long.Parse(node!.ToString(), CultureInfo.InvariantCulture)),
            "stringValue" or "doubleValue" or "boolValue" => node!.DeepClone(),
            _ => throw new NotSupportedException($"OTLP array element {kind}"),
        };
    }
}
