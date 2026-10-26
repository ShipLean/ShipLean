using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Events;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Persisted;
using JiraWorld = ShipLean.WebApi.Tests.Import.Mapping.JiraWorld;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// A dependency that throws inside a run must end the job as Failed, never leave it Running (a running job can be neither stopped nor re-run).
[TestFixture]
public class ImportStuckTests : ImportJobsFixture
{
    protected override IReadOnlyList<JiraIssue> Issues { get; } = [JiraWorld.Issue("PLAT-1")];

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        Pin(services);
        services.AddSingleton<IProjectEvents>(sp => new ThrowingProjectEvents(ActivatorUtilities.CreateInstance<InMemoryProjectEvents>(sp)));
    };

    [Test]
    public async Task Import_WhenAStepOfTheRunThrows_ShouldEndFailedNotRunning()
    {
        var job = await StartAsync("STK");
        await WaitForAsync(job.Id, ImportJobState.Done);
        var events = (ThrowingProjectEvents)Host.Services.GetRequiredService<IProjectEvents>();
        events.Armed = true;
        try
        {
            await PostJobAsync($"/api/import/jobs/{job.Id}/rerun", new { });

            var failed = await WaitForAsync(job.Id, ImportJobState.Failed);
            failed.FinishedAt.ShouldNotBeNull();
        }
        finally
        {
            events.Armed = false;
        }

        (await PostJobAsync($"/api/import/jobs/{job.Id}/rerun", new { })).Status.ShouldBe(ImportJobState.Queued);
        await WaitForAsync(job.Id, ImportJobState.Done);
    }
}
