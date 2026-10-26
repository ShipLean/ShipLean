using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// Jira as the in-memory client answers it, except that reading the comments of one issue waits until the test lets it go: a run can be held in the middle of its first batch.
internal sealed class GatedJiraClient(IJiraClient inner, string gatedIssueKey) : IJiraClient
{
    private readonly TaskCompletionSource _reached = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource _released = new(TaskCreationOptions.RunContinuationsAsynchronously);

    private volatile bool _armed;

    // Until armed the client answers straight through (the mapping step reads comments too).
    public void Arm() => _armed = true;

    public Task Reached => _reached.Task;

    public void Release() => _released.TrySetResult();

    public async Task<IReadOnlyList<JiraComment>> GetCommentsAsync(JiraCredentials credentials, string issueKey, CancellationToken cancellationToken = default)
    {
        if (_armed && issueKey == gatedIssueKey)
        {
            _reached.TrySetResult();
            await _released.Task;
        }

        return await inner.GetCommentsAsync(credentials, issueKey, cancellationToken);
    }

    public Task<JiraUser> GetMyselfAsync(JiraCredentials credentials, CancellationToken cancellationToken = default) => inner.GetMyselfAsync(credentials, cancellationToken);

    public Task<IReadOnlyList<JiraProject>> ListProjectsAsync(JiraCredentials credentials, CancellationToken cancellationToken = default) => inner.ListProjectsAsync(credentials, cancellationToken);

    public Task<IReadOnlyList<JiraIssueType>> GetIssueTypesAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken = default)
        => inner.GetIssueTypesAsync(credentials, projectKey, cancellationToken);

    public Task<IReadOnlyList<JiraStatus>> GetStatusesAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken = default)
        => inner.GetStatusesAsync(credentials, projectKey, cancellationToken);

    public Task<IReadOnlyList<JiraField>> GetFieldsAsync(JiraCredentials credentials, CancellationToken cancellationToken = default) => inner.GetFieldsAsync(credentials, cancellationToken);

    public Task<JiraIssuePage> SearchIssuesAsync(
        JiraCredentials credentials, string projectKey, string? continuation, int pageSize, IReadOnlyList<string> numberFieldIds, CancellationToken cancellationToken = default)
        => inner.SearchIssuesAsync(credentials, projectKey, continuation, pageSize, numberFieldIds, cancellationToken);

    public Task<Stream> OpenAttachmentAsync(JiraCredentials credentials, JiraAttachment attachment, CancellationToken cancellationToken = default)
        => inner.OpenAttachmentAsync(credentials, attachment, cancellationToken);
}
