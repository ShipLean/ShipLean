namespace ShipLean.Application.Features.Links.GitLab;

// The fields of GET /projects/{path}/merge_requests/{iid} and /issues/{iid} that a card uses (an issue has no draft).
public sealed record GitLabReply(string? Title, GitLabUserReply? Author, string? State, bool? Draft);
