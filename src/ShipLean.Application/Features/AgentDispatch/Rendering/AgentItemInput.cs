namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// The optional trailing fields are the item's full details (SHIP-204); a null list is an empty one. Points is story points, Sprint the sprint's name.
public sealed record AgentItemInput(
    string Id,
    string Key,
    string Title,
    string? Description,
    string Url,
    IReadOnlyList<string> Tags,
    IReadOnlyList<AgentCriterionInput> Criteria,
    string? Type = null,
    string? State = null,
    string? Assignee = null,
    string? Sprint = null,
    decimal? Points = null,
    IReadOnlyList<AgentLinkInput>? Links = null,
    IReadOnlyList<AgentChildInput>? Children = null,
    IReadOnlyList<AgentAttachmentInput>? Attachments = null);
