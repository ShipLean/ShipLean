using System.Text.Json.Serialization;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Tools.Agents;

// One agent session: how it ended, its activities newest first (next_before pages to older ones), and its delivery attempts
// oldest first. For a viewer who may not see delivery attempts, deliveries is empty; run_log is null unless they are a project admin.
public sealed record GetAgentSessionResult(
    string? Project,
    string OpenUrl,
    [property: JsonPropertyName("session")] GetAgentSessionResult.SessionView Session,
    [property: JsonPropertyName("activities")] IReadOnlyList<GetAgentSessionResult.ActivityView> Activities,
    [property: JsonPropertyName("next_before")] string? NextBefore,
    [property: JsonPropertyName("deliveries")] IReadOnlyList<GetAgentSessionResult.DeliveryView> Deliveries) : McpResult(Project, OpenUrl)
{
    public sealed record SessionView(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("item")] string Item,
        [property: JsonPropertyName("connection")] string Connection,
        [property: JsonPropertyName("state")] string State,
        [property: JsonPropertyName("started")] string Started,
        [property: JsonPropertyName("ended")] string? Ended,
        [property: JsonPropertyName("end_reason")] string? EndReason,
        [property: JsonPropertyName("end_text")] string? EndText,
        [property: JsonPropertyName("end_detail")] string? EndDetail,
        [property: JsonPropertyName("run_log")] RunLogView? RunLog);

    // attachment is the attachment's name, for read_attachment.
    public sealed record RunLogView(
        [property: JsonPropertyName("attachment")] string Attachment,
        [property: JsonPropertyName("size_bytes")] long SizeBytes);

    public sealed record ActivityView(
        [property: JsonPropertyName("id")] Guid Id,
        [property: JsonPropertyName("type")] string Type,
        [property: JsonPropertyName("body")] string? Body,
        [property: JsonPropertyName("action")] string? Action,
        [property: JsonPropertyName("parameter")] string? Parameter,
        [property: JsonPropertyName("result")] string? Result,
        [property: JsonPropertyName("truncated")] bool Truncated,
        [property: JsonPropertyName("at")] string At);

    public sealed record DeliveryView(
        [property: JsonPropertyName("action")] string Action,
        [property: JsonPropertyName("attempt")] int Attempt,
        [property: JsonPropertyName("at")] string At,
        [property: JsonPropertyName("http_status")] int? HttpStatus,
        [property: JsonPropertyName("error")] string? Error,
        [property: JsonPropertyName("duration_ms")] int DurationMs,
        [property: JsonPropertyName("next_attempt_at")] string? NextAttemptAt);
}
