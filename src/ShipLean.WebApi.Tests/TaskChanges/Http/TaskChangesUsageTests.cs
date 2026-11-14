using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Usage;

namespace ShipLean.WebApi.Tests.TaskChanges.Http;

// Opening the Changes page counts as task_changes.viewed; the Task page's section read is counted by the screen view, so it emits nothing.
[TestFixture]
public class TaskChangesUsageTests : ApiFixture
{
    private static readonly string Item = $"api/work-items/{SeedIds.WorkItems.Ship101}";

    private RecordingUsageEvents Recorder => (RecordingUsageEvents)Host.Services.GetRequiredService<IUsageEvents>();

    [SetUp]
    public void ClearRecordedEvents() => Recorder.Clear();

    [Test]
    public async Task Changes_WhenFetched_ShouldTrackTaskChangesViewed()
    {
        using var response = await Api.GetAsync($"{Item}/changes?commit=all");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        var tracked = Recorder.Events.Where(e => e.Source == "api:TaskChanges.Page").ShouldHaveSingleItem();
        tracked.Name.ShouldBe(UsageEventNames.TaskChangesViewed);
        tracked.Surface.ShouldBe(UsageSurface.Web);
        tracked.ProjectId.ShouldBe(SeedIds.Projects.Ship);
    }

    [Test]
    public async Task Commits_WhenFetched_ShouldTrackNothing()
    {
        using var response = await Api.GetAsync($"{Item}/commits");

        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        Recorder.Events.Where(e => e.Source == "api:TaskChanges.Section").ShouldBeEmpty();
    }
}
