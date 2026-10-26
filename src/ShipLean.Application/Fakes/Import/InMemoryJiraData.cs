using ShipLean.Contracts.Import.Jira;

namespace ShipLean.Application.Fakes.Import;

// The Jira world an InMemoryJiraClient answers from. Issues are per project key, comments per issue key, attachment bytes per attachment id.
public sealed record InMemoryJiraData(
    JiraUser Myself,
    IReadOnlyList<JiraProject> Projects,
    IReadOnlyDictionary<string, IReadOnlyList<JiraIssueType>> IssueTypes,
    IReadOnlyDictionary<string, IReadOnlyList<JiraStatus>> Statuses,
    IReadOnlyList<JiraField> Fields,
    IReadOnlyDictionary<string, IReadOnlyList<JiraIssue>> Issues,
    IReadOnlyDictionary<string, IReadOnlyList<JiraComment>> Comments,
    IReadOnlyDictionary<string, byte[]> Attachments)
{
    public static InMemoryJiraData Empty { get; } = new(
        new JiraUser("0", "Nobody", null, false), [],
        new Dictionary<string, IReadOnlyList<JiraIssueType>>(), new Dictionary<string, IReadOnlyList<JiraStatus>>(), [],
        new Dictionary<string, IReadOnlyList<JiraIssue>>(), new Dictionary<string, IReadOnlyList<JiraComment>>(), new Dictionary<string, byte[]>());
}
