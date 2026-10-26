using System.Globalization;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;

namespace ShipLean.Application.Fakes.Import;

// Answers from typed data. The credentials it accepts are fixed at construction; Down makes every call fail as an unreachable Jira does.
// The continuation token is the index of the next issue.
public sealed class InMemoryJiraClient(JiraCredentials accepted, InMemoryJiraData data) : IJiraClient
{
    private volatile bool _down;

    public bool Down
    {
        get => _down;
        set => _down = value;
    }

    public Task<JiraUser> GetMyselfAsync(JiraCredentials credentials, CancellationToken cancellationToken = default)
        => Answer(credentials, () => data.Myself);

    public Task<IReadOnlyList<JiraProject>> ListProjectsAsync(JiraCredentials credentials, CancellationToken cancellationToken = default)
        => Answer(credentials, () => data.Projects);

    public Task<IReadOnlyList<JiraIssueType>> GetIssueTypesAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken = default)
        => Answer(credentials, () => ForProject(data.IssueTypes, projectKey));

    public Task<IReadOnlyList<JiraStatus>> GetStatusesAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken = default)
        => Answer(credentials, () => ForProject(data.Statuses, projectKey));

    public Task<IReadOnlyList<JiraField>> GetFieldsAsync(JiraCredentials credentials, CancellationToken cancellationToken = default)
        => Answer(credentials, () => data.Fields);

    public Task<JiraIssuePage> SearchIssuesAsync(JiraCredentials credentials, string projectKey, string? continuation, int pageSize, IReadOnlyList<string> numberFieldIds, CancellationToken cancellationToken = default)
        => Answer(credentials, () =>
        {
            var issues = ForProject(data.Issues, projectKey);
            var start = continuation is null ? 0 : int.Parse(continuation, CultureInfo.InvariantCulture);
            var page = issues.Skip(start).Take(Math.Max(1, pageSize))
                .Select(i => i with { Numbers = i.Numbers.Where(n => numberFieldIds.Contains(n.Key)).ToDictionary(n => n.Key, n => n.Value) }).ToList();
            var next = start + page.Count;
            return new JiraIssuePage(page, next < issues.Count ? next.ToString(CultureInfo.InvariantCulture) : null);
        });

    public Task<IReadOnlyList<JiraComment>> GetCommentsAsync(JiraCredentials credentials, string issueKey, CancellationToken cancellationToken = default)
        => Answer(credentials, () =>
        {
            if (!data.Issues.Values.SelectMany(i => i).Any(i => i.Key == issueKey))
            {
                throw ApiException.NotFound($"Jira has no issue {issueKey}.");
            }
            return data.Comments.TryGetValue(issueKey, out var comments) ? comments : [];
        });

    public Task<Stream> OpenAttachmentAsync(JiraCredentials credentials, JiraAttachment attachment, CancellationToken cancellationToken = default)
    {
        if (!IsOnSite(credentials.BaseUrl, attachment.ContentUrl))
        {
            throw ApiException.Validation($"The attachment {attachment.Filename} is not on {credentials.BaseUrl}, so ShipLean will not send the Jira token there.");
        }
        return Answer(credentials, () => data.Attachments.TryGetValue(attachment.Id, out var bytes)
            ? (Stream)new MemoryStream(bytes, writable: false)
            : throw ApiException.NotFound($"Jira no longer has the attachment {attachment.Filename}."));
    }

    private static bool IsOnSite(string baseUrl, string contentUrl)
        => Uri.TryCreate(baseUrl, UriKind.Absolute, out var site) && Uri.TryCreate(contentUrl, UriKind.Absolute, out var content)
            && site.Scheme == content.Scheme && string.Equals(site.Host, content.Host, StringComparison.OrdinalIgnoreCase) && site.Port == content.Port;

    private Task<T> Answer<T>(JiraCredentials credentials, Func<T> read)
    {
        if (_down)
        {
            throw ApiException.NotConfigured($"Jira at {credentials.BaseUrl} did not answer.");
        }
        if (credentials.Token != accepted.Token || (accepted.Kind == JiraKind.Cloud && credentials.Email != accepted.Email))
        {
            throw ApiException.Validation("Jira refused the email and token. Check the email belongs to the account that created the token and that the token has not expired.");
        }
        return Task.FromResult(read());
    }

    private static IReadOnlyList<T> ForProject<T>(IReadOnlyDictionary<string, IReadOnlyList<T>> byProject, string projectKey)
        => byProject.TryGetValue(projectKey, out var list) ? list : throw ApiException.NotFound($"Jira has no project {projectKey}.");
}
