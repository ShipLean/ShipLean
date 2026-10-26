using ShipLean.Contracts;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Import.Jira;

// The project routes of the Jira connection: project admins only.
public sealed class JiraRows : IAccessSweepRows
{
    private const string SaveBody = """{"baseUrl":"https://platform.atlassian.net","kind":"cloud","email":"priya.shah@acme.io","token":"sweep-token-0000"}""";

    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{id:guid}/jira/connection", ProjectRole.Admin),
        new("PUT", "api/projects/{id:guid}/jira/connection", ProjectRole.Admin, Body: SaveBody),
        new("POST", "api/projects/{id:guid}/jira/connection/test", ProjectRole.Admin),
        new("GET", "api/projects/{id:guid}/jira/projects", ProjectRole.Admin),
    ];
}
