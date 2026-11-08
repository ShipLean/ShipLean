using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// What only a propose session's promptContext adds: the GitHub issue ("owner/name", number, the author's login) and the related
// items the starter was given. The issue's title and body travel as the item's title and description, read from github_issue.
public sealed record AgentProposeInput(string Repository, int Number, string AuthorLogin, IReadOnlyList<AgentRelatedWork> Related);
