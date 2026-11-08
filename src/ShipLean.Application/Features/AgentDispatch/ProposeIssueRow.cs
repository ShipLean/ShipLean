namespace ShipLean.Application.Features.AgentDispatch;

// A propose session's github_issue with the ids and keys its envelope needs (AgentContextLoader.LoadProposeAsync).
internal sealed record ProposeIssueRow(
    Guid Id, Guid ItemId, string ItemKey, Guid ProjectId, string ProjectKey, string ProjectName, string Repository, int Number, string AuthorLogin, string Title, string Body);
