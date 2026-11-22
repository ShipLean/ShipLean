using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// P5: the startup snapshot of the 12 built-ins. The host has already started, so the first read here is the first request after start.
[TestFixture]
public class PromptSnapshotTests : ApiFixture
{
    private async Task<int> RowsAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<int>("SELECT count(*)::int FROM prompt_built_in");
    }

    [Test]
    public async Task Startup_ShouldStoreTwelveBuiltIns()
    {
        (await RowsAsync()).ShouldBe(12);
    }

    [Test]
    public async Task Startup_WhenRunTwice_ShouldNotDuplicate()
    {
        var snapshotter = Host.Services.GetServices<IHostedService>().OfType<PromptBuiltInSnapshotter>().ShouldHaveSingleItem();

        await snapshotter.StartAsync(CancellationToken.None);

        (await RowsAsync()).ShouldBe(12);
    }

    [Test]
    public async Task Startup_ShouldFinishBeforeTheHostServes()
    {
        using var response = await Api.GetAsync($"api/projects/{SeedIds.Projects.Ship}/prompts");

        response.IsSuccessStatusCode.ShouldBeTrue();
        (await RowsAsync()).ShouldBe(12);
    }
}
