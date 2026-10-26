using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;

namespace ShipLean.Application.Features.Import.Apply;

// Applies Jira issues to a ShipLean project one at a time; the job runner drives it. Everything is idempotent on the Jira key (or comment and attachment id),
// so applying an issue again updates what the first pass wrote. A row that cannot be written is recorded in import_error (the outcome says Failed) and
// never thrown, so the run goes on; only a failure of the run itself (Jira down, a database error) throws.
public interface IImportApplier
{
    ImportRun Begin(Guid projectId, Guid jobId, JiraCredentials credentials, ImportMappingDto mapping);

    // Creates the states the mapping says are missing (with their board columns) and the sprints, before the issues. Failures throw.
    Task PrepareAsync(ImportRun run, IReadOnlyList<JiraSprint> jiraSprints, CancellationToken cancellationToken = default);

    // The issue as a work item, through IWorkItemStore.ImportAsync: type, state, assignee, reporter, sprint, points, tags, links, parent
    // and changelog. A parent or link to an issue not applied yet is left out and named in UnresolvedKeys.
    Task<ApplyOutcome> ApplyIssueAsync(ImportRun run, JiraIssue issue, CancellationToken cancellationToken = default);

    // The issue's comments, through ICommentStore.ImportAsync. A no-op when the mapping leaves comments out. Returns how many were written.
    Task<int> ApplyCommentsAsync(ImportRun run, JiraIssue issue, Guid itemId, CancellationToken cancellationToken = default);

    // The issue's attachments, bytes read from Jira, through IAttachmentStore.ImportAsync. A no-op when the mapping leaves attachments out. Returns how many were written.
    Task<int> ApplyAttachmentsAsync(ImportRun run, JiraIssue issue, Guid itemId, CancellationToken cancellationToken = default);
}
