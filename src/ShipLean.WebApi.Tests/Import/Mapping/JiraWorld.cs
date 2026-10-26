using ShipLean.Application.Fakes.Import;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;

namespace ShipLean.WebApi.Tests.Import.Mapping;

// A small Jira site for the mapping and apply tests: project PLAT with Epic, Story, Bug and Sub-task types, the statuses they use, two story points fields.
internal static class JiraWorld
{
    public const string ProjectKey = "PLAT";
    public const string PointsField = "customfield_10016";
    public const string OtherPointsField = "customfield_10028";

    public static readonly JiraCredentials Credentials = new("https://acme.example.test", JiraKind.Cloud, "priya@acme.io", "cloud-token");

    public static readonly DateTimeOffset Day = new(2026, 3, 2, 9, 0, 0, TimeSpan.Zero);

    public static readonly JiraIssueType[] IssueTypes =
    [
        new("10000", "Epic", false), new("10001", "Story", false), new("10002", "Bug", false), new("10003", "Sub-task", true),
    ];

    public static JiraUser Person(string account, string name, string? email, bool bot = false) => new(account, name, email, bot);

    public static JiraStatus Status(string name, JiraStatusCategory category, params string[] types)
        => new(name, name, category, types.Length == 0 ? ["Epic", "Story", "Bug", "Sub-task"] : types);

    public static JiraIssue Issue(
        string key, string type = "Story", string status = "To Do", JiraUser? assignee = null, JiraUser? reporter = null, string? parent = null,
        decimal? points = null, IReadOnlyList<JiraSprint>? sprints = null, IReadOnlyList<JiraChangelogEntry>? changelog = null,
        IReadOnlyList<JiraAttachment>? attachments = null, IReadOnlyList<JiraIssueLink>? links = null, string? priority = null)
        => new(
            key, $"id-{key}", type, status, $"Summary of {key}", $"Description of {key}", assignee, reporter, priority,
            points is { } p ? new Dictionary<string, decimal> { [PointsField] = p } : new Dictionary<string, decimal>(),
            sprints ?? [], parent, [], [], Day, Day.AddDays(3), links ?? [], attachments ?? [], changelog ?? []);

    public static InMemoryJiraData Data(
        IReadOnlyList<JiraIssue> issues, IReadOnlyList<JiraStatus>? statuses = null, IReadOnlyDictionary<string, IReadOnlyList<JiraComment>>? comments = null,
        IReadOnlyDictionary<string, byte[]>? attachmentBytes = null)
        => new(
            Person("0", "Priya", "priya@acme.io"),
            [new JiraProject(ProjectKey, "Platform", "software", issues.Count)],
            new Dictionary<string, IReadOnlyList<JiraIssueType>> { [ProjectKey] = IssueTypes },
            new Dictionary<string, IReadOnlyList<JiraStatus>>
            {
                [ProjectKey] = statuses ??
                [
                    Status("To Do", JiraStatusCategory.New), Status("In Progress", JiraStatusCategory.Indeterminate), Status("Done", JiraStatusCategory.Done),
                ],
            },
            [new JiraField(PointsField, "Story Points", true, "number"), new JiraField(OtherPointsField, "Story point estimate", true, "number"), new JiraField("summary", "Summary", false, "string")],
            new Dictionary<string, IReadOnlyList<JiraIssue>> { [ProjectKey] = issues },
            comments ?? new Dictionary<string, IReadOnlyList<JiraComment>>(),
            attachmentBytes ?? new Dictionary<string, byte[]>());
}
