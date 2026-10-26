using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Search.Index.Wake;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Search;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Index.Service;

// A burst of events becomes one deduplicated set of sources.
[TestFixture]
public class SearchWakeSourcesTests : ApiFixture
{
    [Test]
    public async Task Wake_WhenManyEventsName_ShouldReturnTheUnionOnce()
    {
        var project = ShipLean.TestSupport.SeedIds.Projects.Ship;
        var item = Guid.NewGuid();
        var sprint = Guid.NewGuid();

        var sources = await Host.Services.GetRequiredService<SearchWakeSources>().ForAsync(
        [
            new WorkItemUpdated(project, item, 2, [], false),
            new WorkItemUpdated(project, item, 3, [], false),
            new SprintChanged(project, sprint),
            new SprintChanged(project, sprint),
            new ProjectChanged(project),
        ]);

        sources.ShouldBe(
            [new SearchSource(SearchSourceKind.Item, item), new SearchSource(SearchSourceKind.Sprint, sprint), new SearchSource(SearchSourceKind.Project, project)],
            ignoreOrder: true);
    }
}
