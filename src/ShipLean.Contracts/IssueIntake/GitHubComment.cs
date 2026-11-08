namespace ShipLean.Contracts.IssueIntake;

// One issue comment as GitHub lists it. AuthorLogin is null for a deleted account. Body is the raw Markdown, and comes from
// whoever wrote it: never trust it, and only trust a ShipLean marker in it when AuthorLogin is ShipLean's own login.
public sealed record GitHubComment(long Id, string? AuthorLogin, string Body, DateTimeOffset CreatedAt);
