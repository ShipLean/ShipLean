using ShipLean.Application.Fakes.Import;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.TestSupport.Contracts.Import;

namespace ShipLean.UnitTest.Contracts.Import;

// A small Jira world for the in-memory client: three issues of PLATFORM (an epic, a story with comments and history, a bug with an attachment).
internal static class InMemoryJiraWorld
{
    private const string Project = "PLATFORM";
    private const string AttachmentId = "10601";
    private static readonly byte[] Bytes = [0x89, 0x50, 0x4e, 0x47, 1, 2, 3, 4];
    private static readonly JiraUser Priya = new("acc-priya", "Priya Shah", "priya.shah@acme.io", false);
    private static readonly JiraSprint Sprint10 = new(110, "Platform Sprint 10", "closed", new DateTimeOffset(2026, 8, 3, 1, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 8, 31, 10, 0, 0, TimeSpan.Zero), "Cut the query latency in half");
    private static readonly JiraSprint Sprint11 = new(111, "Platform Sprint 11", "closed", new DateTimeOffset(2026, 8, 31, 1, 0, 0, TimeSpan.Zero), new DateTimeOffset(2026, 9, 28, 10, 0, 0, TimeSpan.Zero), "Ship the scoring service behind a flag");
    private static readonly JiraSprint Sprint13 = new(113, "Platform Sprint 13", "future", null, null, null);
    private static readonly JiraUser Tom = new("acc-tom", "Tom Okafor", "tom.okafor@acme.io", false);

    public static JiraWorld Create(JiraKind kind, JiraFault fault)
    {
        var accepted = kind == JiraKind.Cloud
            ? new JiraCredentials("https://platform.example.test", kind, "priya.shah@acme.io", "cloud-token")
            : new JiraCredentials("https://jira.example.test", kind, string.Empty, "server-token");
        var client = new InMemoryJiraClient(accepted, Data(kind, accepted.BaseUrl)) { Down = fault == JiraFault.Down };
        var credentials = fault == JiraFault.WrongToken ? accepted with { Token = "not-the-token" } : accepted;
        return new JiraWorld(credentials, client, Project, ["PLATFORM-403", "PLATFORM-404", "PLATFORM-406", "PLATFORM-410"], "PLATFORM-404", 2, Bytes, "PLATFORM-403", "PLATFORM-404", kind == JiraKind.Cloud ? "acc-automation" : null);
    }

    private static InMemoryJiraData Data(JiraKind kind, string baseUrl)
    {
        var created = new DateTimeOffset(2026, 9, 1, 8, 0, 0, TimeSpan.Zero);
        var automation = new JiraUser("acc-automation", "Automation for Jira", null, kind == JiraKind.Cloud);
        var history = new[]
        {
            Change("1", created.AddDays(1), "Backlog", "In Progress"),
            new JiraChangelogEntry("3", automation, created.AddDays(4), [new JiraChangeItem("assignee", string.Empty, "Tom Okafor")]),
            Change("2", created.AddDays(8), "In Progress", "Done"),
        };
        var issues = new[]
        {
            Issue("PLATFORM-403", "Epic", "In Progress", created, [], [], [], Points(null, null)),
            Issue("PLATFORM-404", "Story", "Done", created, history, [], [Sprint10, Sprint11], Points(5m, 6.5m), "PLATFORM-403"),
            Issue("PLATFORM-406", "Bug", "Done", created, [], [new JiraAttachment(AttachmentId, "load-test.png", "image/png", Bytes.Length, baseUrl + (kind == JiraKind.Cloud ? "/rest/api/3/attachment/content/10601" : "/secure/attachment/10601/load-test.png"))], [Sprint11], Points(2m, null)),
            Issue("PLATFORM-410", "Story", "Backlog", created, [], [], [Sprint13], Points(null, null)),
        };
        return new InMemoryJiraData(
            Priya,
            [new JiraProject(Project, "Platform Team", "software", issues.Length)],
            new Dictionary<string, IReadOnlyList<JiraIssueType>>
            {
                [Project] = [new("10000", "Epic", false), new("10001", "Story", false), new("10004", "Bug", false), new("10003", "Sub-task", true)],
            },
            new Dictionary<string, IReadOnlyList<JiraStatus>>
            {
                [Project] =
                [
                    new("10000", "Backlog", JiraStatusCategory.New, ["Story", "Bug"]),
                    new("3", "In Progress", JiraStatusCategory.Indeterminate, ["Epic", "Story", "Bug"]),
                    new("10004", "Done", JiraStatusCategory.Done, ["Epic", "Story", "Bug"]),
                ],
            },
            [new("customfield_10016", "Story point estimate", true, "number")],
            new Dictionary<string, IReadOnlyList<JiraIssue>> { [Project] = issues },
            new Dictionary<string, IReadOnlyList<JiraComment>>
            {
                ["PLATFORM-404"] =
                [
                    new("30401", Priya, "Please also post the run time in the summary.", created.AddDays(2), created.AddDays(2)),
                    new("30402", Tom, "Done: the summary shows the run time.", created.AddDays(3), created.AddDays(3)),
                ],
            },
            new Dictionary<string, byte[]> { [AttachmentId] = Bytes });
    }

    private static JiraChangelogEntry Change(string id, DateTimeOffset when, string from, string to)
        => new(id, Tom, when, [new JiraChangeItem("status", from, to)]);

    private static Dictionary<string, decimal> Points(decimal? storyPoints, decimal? classic)
    {
        var numbers = new Dictionary<string, decimal>();
        if (storyPoints is { } points)
        {
            numbers["customfield_10016"] = points;
        }
        if (classic is { } value)
        {
            numbers["customfield_10028"] = value;
        }
        return numbers;
    }

    private static JiraIssue Issue(
        string key, string type, string status, DateTimeOffset created, IReadOnlyList<JiraChangelogEntry> changelog,
        IReadOnlyList<JiraAttachment> attachments, IReadOnlyList<JiraSprint> sprints, IReadOnlyDictionary<string, decimal> numbers, string? parentKey = null)
        => new(key, "1" + key[^3..], type, status, "Summary of " + key, "Plain description of " + key, Tom, Priya, "Medium", numbers,
            sprints, parentKey, [], [], created, created.AddDays(10), [], attachments, changelog);
}
