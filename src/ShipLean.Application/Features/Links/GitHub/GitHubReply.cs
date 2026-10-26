namespace ShipLean.Application.Features.Links.GitHub;

// The fields of GET /repos/{owner}/{repo}/pulls/{n} and /issues/{n} that a card uses (an issue has no draft or merged).
public sealed record GitHubReply(string? Title, GitHubUserReply? User, string? State, bool? Draft, bool? Merged);
