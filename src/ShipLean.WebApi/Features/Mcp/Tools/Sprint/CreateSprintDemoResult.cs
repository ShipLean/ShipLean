using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// create_sprint_demo: the upload link, the curl that uses it and the brand. The prompt itself is the result's Markdown.
public sealed record CreateSprintDemoResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("sprint")] string Sprint,
    [property: JsonPropertyName("demo_item")] string DemoItem,
    [property: JsonPropertyName("item_url")] string ItemUrl,
    [property: JsonPropertyName("upload_url")] string UploadUrl,
    [property: JsonPropertyName("upload_expires_at")] DateTimeOffset UploadExpiresAt,
    [property: JsonPropertyName("max_video_bytes")] long MaxVideoBytes,
    [property: JsonPropertyName("command")] string Command,
    [property: JsonPropertyName("brand")] CreateSprintDemoBrand Brand) : McpResult(Project, OpenUrl);
