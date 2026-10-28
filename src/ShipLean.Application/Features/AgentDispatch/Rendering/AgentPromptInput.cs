namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// The follow-up behind a `prompted` event: a person's comment (CommentId set) or the Stop button (Signal "stop", empty Body).
public sealed record AgentPromptInput(
    string ActivityId,
    AgentUserInput Author,
    DateTimeOffset CreatedAt,
    string Body,
    string? CommentId,
    string? Signal)
{
    public const string StopSignal = "stop";
}
