using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;

namespace ShipLean.Application.Features.Import.Mapping;

// What one pass over the Jira project's issues counted: the figures the mapping rows and the totals strip show.
internal sealed class JiraSurvey
{
    private readonly Dictionary<string, int> _types = [];
    private readonly Dictionary<(string Type, string Status), int> _statuses = [];
    private readonly Dictionary<string, (JiraUser User, int References)> _users = [];
    private readonly Dictionary<string, int> _populated = [];
    private readonly HashSet<long> _sprints = [];

    public int Issues { get; private set; }

    public int Comments { get; private set; }

    public int Attachments { get; private set; }

    public int Revisions { get; private set; }

    public int SprintCount => _sprints.Count;

    public int TypeCount(string type) => _types.GetValueOrDefault(type);

    public int StatusCount(string type, string status) => _statuses.GetValueOrDefault((type, status));

    public int Populated(string fieldId) => _populated.GetValueOrDefault(fieldId);

    public IEnumerable<(JiraUser User, int References)> Users => _users.Values;

    public static async Task<JiraSurvey> ReadAsync(
        IJiraClient client, JiraCredentials credentials, string projectKey, IReadOnlyList<string> numberFieldIds, bool readComments, CancellationToken cancellationToken)
    {
        var survey = new JiraSurvey();
        string? continuation = null;
        do
        {
            var page = await client.SearchIssuesAsync(credentials, projectKey, continuation, 100, numberFieldIds, cancellationToken);
            foreach (var issue in page.Issues)
            {
                IReadOnlyList<JiraComment> comments = readComments ? await client.GetCommentsAsync(credentials, issue.Key, cancellationToken) : [];
                survey.Add(issue, comments);
            }

            continuation = page.Continuation;
        }
        while (continuation is not null);
        return survey;
    }

    private void Add(JiraIssue issue, IReadOnlyList<JiraComment> comments)
    {
        Issues++;
        _types[issue.Type] = TypeCount(issue.Type) + 1;
        _statuses[(issue.Type, issue.Status)] = StatusCount(issue.Type, issue.Status) + 1;
        Comments += comments.Count;
        Attachments += issue.Attachments.Count;
        Revisions += issue.Changelog.Count;
        foreach (var sprint in issue.Sprints)
        {
            _sprints.Add(sprint.Id);
        }

        foreach (var id in issue.Numbers.Keys)
        {
            _populated[id] = Populated(id) + 1;
        }

        foreach (var user in new[] { issue.Assignee, issue.Reporter }
            .Concat(comments.Select(c => c.Author))
            .Concat(issue.Changelog.Select(c => c.Author))
            .OfType<JiraUser>())
        {
            _users[user.AccountId] = (user, _users.TryGetValue(user.AccountId, out var seen) ? seen.References + 1 : 1);
        }
    }
}
