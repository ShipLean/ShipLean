using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// A re-run carries pending errors over, and an item that still fails is still one pending row, not one more per run.
[TestFixture]
public class ImportRerunErrorsTests : ImportJobsFixture
{
    private static readonly JiraAttachment Never = new("att-9", "never.txt", "text/plain", 5, "https://acme.example.test/secure/attachment/att-9/never.txt");

    protected override IReadOnlyList<JiraIssue> Issues { get; } = [JiraWorld.Issue("PLAT-40", attachments: [Never]), JiraWorld.Issue("PLAT-41")];

    [Test]
    public async Task Import_WhenRerunWithPendingErrors_ShouldKeepOnePendingRowPerItem()
    {
        var job = await StartAsync("RRE");
        await WaitForAsync(job.Id, ImportJobState.DoneWithErrors);
        (await PendingAsync(job.Id)).ShouldBe(["PLAT-40/att-9"]);

        await PostJobAsync($"/api/import/jobs/{job.Id}/rerun", new { });
        var again = await WaitForAsync(job.Id, j => j.Status == ImportJobState.DoneWithErrors && j.Progress.RerunResult is not null);
        await PostJobAsync($"/api/import/jobs/{job.Id}/rerun", new { });
        await WaitForAsync(job.Id, j => j.Status == ImportJobState.DoneWithErrors && j.Progress.RerunResult is not null && j.FinishedAt > again.FinishedAt);

        (await PendingAsync(job.Id)).ShouldBe(["PLAT-40/att-9"]);
    }

    private async Task<IReadOnlyList<string>> PendingAsync(Guid jobId)
        => [.. (await ErrorsAsync(jobId)).Where(e => e.Resolution == ImportErrorResolution.Pending).Select(e => e.SourceKey)];
}
