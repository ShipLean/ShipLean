using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// The import routes. The seed has an import job and a Jira connection only in MOB, whose members are Joel (admin), Priya and Tom, and Mei (the sweep's below-minimum caller) is
// not in MOB. So the rows that need an existing job or connection call MOB's and declare Member, which keeps the below-minimum leg (Mei, expecting 403) from reading a 404 where
// she is no member; the Admin rule itself is asserted in ImportAccessTests (the authority for who may do what here), as a MOB member who is not an admin. These Member rows only prove
// that a non-member gets 404 and an admin passes. The project list is a SHIP row and is swept as Admin.
public sealed class ImportJobsRows : IAccessSweepRows
{
    private static readonly string Job = $"api/import/jobs/{SeedIds.ImportJobs.MobImport}";

    // Binds, then fails validation (a blank Jira project) after the access check, so the allowed leg reads a 400 and never starts an import.
    private static readonly string StartBody =
        $$$"""
        {"connectionId":"{{{SeedIds.JiraConnections.Mob}}}","jiraProjectKey":" ","target":{"kind":"newProject","key":"SWPT","name":"Sweep","projectId":null},
         "mapping":{"issueTypes":[],"statuses":[],"users":[],"fields":{"storyPointsFieldId":"","candidates":[],"includeComponents":true,"includeComments":true,"includeAttachments":true,"includeChangelog":true},
         "totals":{"issues":0,"comments":0,"attachments":0,"revisions":0,"sprints":0,"people":0},"warnings":[]}}
        """;

    private static readonly string MappingBody = $$$"""{"connectionId":"{{{SeedIds.JiraConnections.Mob}}}","jiraProjectKey":" ","previousJobId":null}""";

    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{id:guid}/import/jobs", ProjectRole.Admin),
        new("POST", "api/import/jobs", ProjectRole.Member, Body: StartBody),
        new("POST", "api/import/mapping", ProjectRole.Member, Body: MappingBody),
        new("GET", "api/import/jobs/{jobId:guid}", ProjectRole.Member, Url: Job),
        new("POST", "api/import/jobs/{jobId:guid}/stop", ProjectRole.Member, Url: $"{Job}/stop"),
        new("POST", "api/import/jobs/{jobId:guid}/retry", ProjectRole.Member, Body: "{}", Url: $"{Job}/retry"),
        new("POST", "api/import/jobs/{jobId:guid}/skip", ProjectRole.Member, Body: "{}", Url: $"{Job}/skip"),
        new("POST", "api/import/jobs/{jobId:guid}/rerun", ProjectRole.Member, Body: "{}", Url: $"{Job}/rerun"),
        new("GET", "api/import/jobs/{jobId:guid}/log", ProjectRole.Member, Url: $"{Job}/log"),
        new("GET", "api/import/jobs/{jobId:guid}/errors", ProjectRole.Member, Url: $"{Job}/errors"),
    ];
}
