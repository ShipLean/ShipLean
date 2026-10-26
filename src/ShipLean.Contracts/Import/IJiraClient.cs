using ShipLean.Contracts.Import.Jira;

namespace ShipLean.Contracts.Import;

// What ShipLean asks of Jira; read-only and stateless. Every method throws ApiException.Validation ("Jira refused the email and token...") for bad credentials and ApiException.NotConfigured when Jira cannot be reached or answers 5xx.
public interface IJiraClient
{
    // The signed-in Jira user.
    Task<JiraUser> GetMyselfAsync(JiraCredentials credentials, CancellationToken cancellationToken = default);

    // Projects the credentials can browse.
    Task<IReadOnlyList<JiraProject>> ListProjectsAsync(JiraCredentials credentials, CancellationToken cancellationToken = default);

    // Also throws ApiException.NotFound for an unknown project key (all the project-scoped calls below do).
    Task<IReadOnlyList<JiraIssueType>> GetIssueTypesAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JiraStatus>> GetStatusesAsync(JiraCredentials credentials, string projectKey, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<JiraField>> GetFieldsAsync(JiraCredentials credentials, CancellationToken cancellationToken = default);

    // Issues in key order with changelog, sprints (with dates) and attachments, one page at a time; continuation is null for the first page. Each issue's Numbers holds the values of the requested
    // number fields (customfield ids, e.g. the story points field the user chose) that it has. Jira answers an unknown project with a 400 ("The value 'NOPE' does not exist for the field 'project'."):
    // that one text is NotFound, but not every 400 is (a bad token or query is Validation).
    Task<JiraIssuePage> SearchIssuesAsync(JiraCredentials credentials, string projectKey, string? continuation, int pageSize, IReadOnlyList<string> numberFieldIds, CancellationToken cancellationToken = default);

    // Oldest first. Throws ApiException.NotFound for an unknown issue.
    Task<IReadOnlyList<JiraComment>> GetCommentsAsync(JiraCredentials credentials, string issueKey, CancellationToken cancellationToken = default);

    // The attachment's bytes; the caller disposes the stream. Throws ApiException.Validation when the attachment's ContentUrl has another scheme, host or port than the credentials' BaseUrl (a stored or crafted URL must not
    // receive the token), and ApiException.NotFound for an attachment Jira no longer has. Redirects are followed (Jira Cloud answers 303 to Atlassian's media service, another host), but the Authorization header is only ever sent to the BaseUrl's origin, and only over https: never to a redirect target elsewhere.
    Task<Stream> OpenAttachmentAsync(JiraCredentials credentials, JiraAttachment attachment, CancellationToken cancellationToken = default);
}
