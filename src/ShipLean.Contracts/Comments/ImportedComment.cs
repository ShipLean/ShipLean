namespace ShipLean.Contracts.Comments;

// A comment from another system. AuthorId is the user the importer matched, or the seeded Jira import user
// (SeedIds.Users.JiraImport) for authors that match nobody; null only when the source has no author at all.
public sealed record ImportedComment(
    string OriginalSystem,
    string OriginalSystemId,
    Guid? AuthorId,
    string Body,
    DateTimeOffset CreatedAt);
