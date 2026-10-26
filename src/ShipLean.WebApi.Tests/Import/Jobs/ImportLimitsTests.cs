using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// A Jira that never reaches a last page cannot hold a run for ever or fill the server's memory: the job fails with a clear message and no project is made.
[TestFixture]
public class ImportLimitsTests : ImportJobsFixture
{
    private EndlessJiraClient? _endless;

    protected override IReadOnlyList<JiraIssue> Issues { get; } = [JiraWorld.Issue("PLAT-1")];

    protected override IJiraClient AppJira => _endless ??= new EndlessJiraClient(Jira);

    [Test]
    public async Task Import_WhenJiraNeverEndsItsPages_ShouldFailJobWithMessage()
    {
        var mapping = await SuggestAsync();
        _endless!.SwitchOn();

        var started = await StartAsync("LIM", mapping);

        var failed = await WaitForAsync(started.Id, ImportJobState.Failed);
        failed.Progress.Message.ShouldContain("more issues than ShipLean imports in one job");
        (await QueryAsync<int>("SELECT count(*)::int FROM project WHERE key = 'LIM'")).ShouldBe(0);
    }
}
