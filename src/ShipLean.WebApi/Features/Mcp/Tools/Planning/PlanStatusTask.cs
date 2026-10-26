using System.Text.Json.Serialization;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

// Category is the state's category (proposed, inProgress, resolved, completed, removed). StartedAt and FinishedAt are UTC: StartedAt is the
// first move into progress, null until then; FinishedAt is the last move into Completed, null unless the task is Completed now. ReviewRounds counts its review comments; Orphan is set when the task's report left it behind.
public sealed record PlanStatusTask(
    [property: JsonPropertyName("key")] string Key,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("assignee")] string? Assignee,
    [property: JsonPropertyName("model")] string? Model,
    [property: JsonPropertyName("context_k")] int? ContextK,
    [property: JsonPropertyName("started_at")] DateTimeOffset? StartedAt,
    [property: JsonPropertyName("finished_at")] DateTimeOffset? FinishedAt,
    [property: JsonPropertyName("commits")] IReadOnlyList<PlanCommit> Commits,
    [property: JsonPropertyName("review_rounds")] int ReviewRounds,
    [property: JsonPropertyName("orphan")] bool Orphan);
