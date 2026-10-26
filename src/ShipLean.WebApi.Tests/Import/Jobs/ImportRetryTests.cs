using System.Text;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// Rows that fail (a Jira attachment that is not there yet) become errors to retry or skip. The retry pages the project's search to find the issue again.
[TestFixture]
public class ImportRetryTests : ImportJobsFixture
{
    private static readonly string Url = "https://acme.example.test/secure/attachment";
    private static readonly JiraAttachment Early = new("att-1", "early.txt", "text/plain", 5, $"{Url}/att-1/early.txt");
    private static readonly JiraAttachment Never = new("att-2", "never.txt", "text/plain", 5, $"{Url}/att-2/never.txt");

    private static Guid _jobId;

    protected override IReadOnlyList<JiraIssue> Issues { get; } =
    [
        JiraWorld.Issue("PLAT-30", attachments: [Early]),
        JiraWorld.Issue("PLAT-31", attachments: [Never]),
    ];

    [Test]
    [Order(1)]
    public async Task Import_WhenAttachmentsFail_ShouldFinishWithPendingErrors()
    {
        _jobId = (await StartAsync("RTYA")).Id;

        var job = await WaitForAsync(_jobId, ImportJobState.DoneWithErrors);

        job.Progress.Counts.Attachments.Failed.ShouldBe(2);
        (await ErrorsAsync(_jobId)).Select(e => e.SourceKey).ShouldBe(["PLAT-30/att-1", "PLAT-31/att-2"]);
    }

    [Test]
    [Order(2)]
    public async Task Import_WhenRowFails_ShouldRetryOrSkip()
    {
        AttachmentBytes["att-1"] = Encoding.UTF8.GetBytes("hello");
        var errors = await ErrorsAsync(_jobId);

        var afterRetry = await PostJobAsync($"/api/import/jobs/{_jobId}/retry", new { errorId = errors[0].Id });

        afterRetry.Status.ShouldBe(ImportJobState.DoneWithErrors);
        Attachments.Imported.Select(a => a.Attachment.OriginalSystemId).ShouldContain("att-1");
        var retried = await ErrorsAsync(_jobId);
        retried.Single(e => e.Id == errors[0].Id).Resolution.ShouldBe(ImportErrorResolution.Retried);

        var stillFailing = await PostJobAsync($"/api/import/jobs/{_jobId}/retry", new { errorId = errors[1].Id });

        stillFailing.Status.ShouldBe(ImportJobState.DoneWithErrors);
        var pending = (await ErrorsAsync(_jobId)).Where(e => e.Resolution == ImportErrorResolution.Pending).ToList();
        pending.Count.ShouldBe(1);
        pending[0].SourceKey.ShouldBe("PLAT-31/att-2");
        pending[0].Id.ShouldNotBe(errors[1].Id);

        var skipped = await PostJobAsync($"/api/import/jobs/{_jobId}/skip", new { errorId = pending[0].Id });

        skipped.Status.ShouldBe(ImportJobState.Done);
        (await ErrorsAsync(_jobId)).ShouldAllBe(e => e.Resolution != ImportErrorResolution.Pending);
    }

    [Test]
    [Order(3)]
    public async Task Import_WhenRetryAllFailsAgain_ShouldKeepOnePendingRow()
    {
        var second = (await StartAsync("RTYB")).Id;
        await WaitForAsync(second, ImportJobState.DoneWithErrors);
        var before = await ErrorsAsync(second);
        before.Count.ShouldBe(1);

        var job = await PostJobAsync($"/api/import/jobs/{second}/retry", new { });

        job.Status.ShouldBe(ImportJobState.DoneWithErrors);
        var after = await ErrorsAsync(second);
        after.Count(e => e.Resolution == ImportErrorResolution.Pending).ShouldBe(1);
        after.Single(e => e.Id == before[0].Id).Resolution.ShouldBe(ImportErrorResolution.Retried);
    }

    [Test]
    [Order(4)]
    public async Task Import_WhenRetryIssueIsGoneFromJira_ShouldStayPendingWithTheReason()
    {
        var third = (await StartAsync("RTYC")).Id;
        await WaitForAsync(third, ImportJobState.DoneWithErrors);
        await using (var database = await OpenDatabaseAsync())
        {
            await Dapper.SqlMapper.ExecuteAsync(
                database, "INSERT INTO import_error (id, import_job_id, step, entity, source_key, message) VALUES (@id, @third, 'issues', 'issue', 'PLAT-999', 'Jira timed out (504)')",
                new { id = Guid.CreateVersion7(), third });
        }

        await PostJobAsync($"/api/import/jobs/{third}/retry", new { });

        var gone = (await ErrorsAsync(third)).Single(e => e.SourceKey == "PLAT-999" && e.Resolution == ImportErrorResolution.Pending);
        gone.Message.ShouldContain("no longer has PLAT-999");
    }
}
