using System.Text.Json;
using System.Text.Json.Serialization;

namespace ShipLean.Contracts.Views.InspectionMetrics;

// The MCP form of InspectionMetricsViewDto (SHIP-1398 K2, K4): the same fields as over HTTP, with snake_case names and snake_case enum values
// (for example ended_by last_commit). Otherwise the house web options.
public static class InspectionMetricsJson
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
