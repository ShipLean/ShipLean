namespace ShipLean.Application.Features.Import.Jobs;

// An import_error row as Dapper reads it. Step and Resolution are the stored text.
public sealed record ImportErrorRow(Guid Id, Guid JobId, string Step, string Entity, string SourceKey, string Message, string Resolution)
{
    public const string Pending = "pending";

    // The Jira issue the row is about: a comment or attachment key looks like KEY/id.
    public string IssueKey => SourceKey.Contains('/', StringComparison.Ordinal) ? SourceKey[..SourceKey.IndexOf('/', StringComparison.Ordinal)] : SourceKey;
}
