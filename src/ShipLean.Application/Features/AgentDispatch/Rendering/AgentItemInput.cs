namespace ShipLean.Application.Features.AgentDispatch.Rendering;

public sealed record AgentItemInput(
    string Id,
    string Key,
    string Title,
    string? Description,
    string Url,
    IReadOnlyList<string> Tags,
    IReadOnlyList<AgentCriterionInput> Criteria);
