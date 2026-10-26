using System.Net;
using ShipLean.Application.Fakes.Import;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// 120 issues are three batches of 50. The run is held inside the first batch, a stop is asked for, and the run ends after that batch.
[TestFixture]
public class ImportStopTests : ImportJobsFixture
{
    private GatedJiraClient? _gate;

    protected override IReadOnlyList<JiraIssue> Issues { get; } = [.. Enumerable.Range(1, 120).Select(n => JiraWorld.Issue($"PLAT-{n}"))];

    protected override IReadOnlyDictionary<string, IReadOnlyList<JiraComment>> Comments { get; } = new Dictionary<string, IReadOnlyList<JiraComment>>
    {
        ["PLAT-1"] = [new JiraComment("c1", JiraWorld.Person("a", "A", "a@acme.io"), "Hello", JiraWorld.Day, JiraWorld.Day)],
    };

    protected override IJiraClient AppJira => _gate ??= new GatedJiraClient(Jira, "PLAT-1");

    [Test]
    public async Task Import_WhenStopAsked_ShouldStopAfterTheCurrentBatch()
    {
        var mapping = await SuggestAsync();
        _gate!.Arm();
        var job = await StartAsync("STP", mapping);
        await _gate.Reached;

        var stopping = await PostJobAsync($"/api/import/jobs/{job.Id}/stop", new { });
        stopping.Status.ShouldBe(ImportJobState.Running);
        _gate.Release();

        var stopped = await WaitForAsync(job.Id, ImportJobState.Stopped);

        stopped.FinishedAt.ShouldNotBeNull();
        stopped.Progress.Message.ShouldBe("Stopped after 50 of 120 issues");
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id", new { id = stopped.ProjectId })).ShouldBe(50);

        await PostJobAsync($"/api/import/jobs/{job.Id}/rerun", new { });
        var done = await WaitForAsync(job.Id, ImportJobState.Done);
        (await QueryAsync<int>("SELECT count(*)::int FROM work_item WHERE project_id = @id", new { id = done.ProjectId })).ShouldBe(120);
    }

    [Test]
    public async Task Import_WhenStopAskedAfterItFinished_ShouldBeConflict()
    {
        var finished = (await GetAsync<IReadOnlyList<ImportJobDto>>("/api/import/jobs")).FirstOrDefault(j => j.Status == ImportJobState.Done);
        finished ??= await WaitForAsync((await StartAsync("STQ")).Id, ImportJobState.Done);

        using var response = await Api.PostJsonAsync($"/api/import/jobs/{finished.Id}/stop", new { });

        response.StatusCode.ShouldBe(HttpStatusCode.Conflict);
    }
}
