using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Views.Timeline;

// The MCP form of FeatureTimelineViewDto (D17): the same fields as over HTTP, with snake_case names and snake_case enum values
// (key_dates, changes_requested, execution_started). Otherwise the house web options.
public static class TimelineJson
{
    public static JsonSerializerOptions McpOptions { get; } = CreateMcpOptions();

    private static JsonSerializerOptions CreateMcpOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        };
        options.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false));
        options.MakeReadOnly(populateMissingResolver: true);
        return options;
    }
}
