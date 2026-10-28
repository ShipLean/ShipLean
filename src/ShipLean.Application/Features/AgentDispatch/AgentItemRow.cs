namespace ShipLean.Application.Features.AgentDispatch;

// The item and its project as the context loader reads them. Criteria is the acceptance_criteria jsonb and Tags the tags array as a jsonb array, both as text.
internal sealed record AgentItemRow(
    Guid Id, string Key, string Title, string Description, string Criteria, string Tags, Guid ProjectId, string ProjectKey, string ProjectName);
