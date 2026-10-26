using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// A hostile or broken Jira: once switched on, its issue search never reaches a last page. Everything else is the in-memory client's.
internal sealed class EndlessJiraClient(IJiraClient inner) : IJiraClient
{
    private volatile bool _endless;

    public void SwitchOn() => _endless = true;

    public Task<JiraIssuePage> SearchIssuesAsync(
        JiraCredentials credentials, string projectKey, string? continuation, int pageSize, IReadOnlyList<string> numberFieldIds, CancellationToken cancellationToken = default)
        => _endless
            ? Task.FromResult(new JiraIssuePage([], "next"))
            : inner.SearchIssuesAsync(credentials, projectKey, continuation, pageSize, numberFieldIds, cancellationToken);

    public Task<IReadOnlyList<JiraComment>> GetCommentsAsync(JiraCredentials credentials, string issueKey, CancellationToken cancellationToken = default)
        => inner.GetCommentsAsync(credentials, issueKey, cancellationToken);

    public Task<JiraUser> GetMyselfAsync(JiraCredentials credentials, CancellationToken cancellationToken = default) => inner.GetMyselfAsync(credentials, cancellationToken);

    public Task<IReadOnlyList<JiraProject>> ListProjectsAsync(JiraCredentials credentials, CancellationToken cancellationToken = default) => inner.ListProjectsAsync(credentials, cancellationToken);

    public Task<IReadOnlyList<JiraIssueType>> GetIssueTypesAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken = default)
        => inner.GetIssueTypesAsync(credentials, projectKey, cancellationToken);

    public Task<IReadOnlyList<JiraStatus>> GetStatusesAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken = default)
        => inner.GetStatusesAsync(credentials, projectKey, cancellationToken);

    public Task<IReadOnlyList<JiraField>> GetFieldsAsync(JiraCredentials credentials, CancellationToken cancellationToken = default) => inner.GetFieldsAsync(credentials, cancellationToken);

    public Task<Stream> OpenAttachmentAsync(JiraCredentials credentials, JiraAttachment attachment, CancellationToken cancellationToken = default)
        => inner.OpenAttachmentAsync(credentials, attachment, cancellationToken);
}
