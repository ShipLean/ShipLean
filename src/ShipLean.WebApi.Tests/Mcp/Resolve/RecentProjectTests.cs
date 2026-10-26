using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Tests.Mcp.Resolve;

// Joel's recentProjects preference starts with a project he is not in, then MOB: a read with no project uses MOB.
[TestFixture]
public class RecentProjectTests : ApiFixture
{
    [OneTimeSetUp]
    public async Task ArrangeRecentProjects()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO user_preference (user_id, key, json) VALUES (@joel, 'recentProjects', @json::jsonb)",
            new { joel = SeedIds.Users.Joel, json = $"[\"{SeedIds.Projects.Part}\", \"{SeedIds.Projects.Mob}\", \"{SeedIds.Projects.Ship}\"]" });
    }

    [Test]
    public async Task Resolve_WhenReadWithoutProject_ShouldUseMostRecent()
    {
        var project = await Host.Services.GetRequiredService<ProjectResolver>().ResolveAsync(null, forWrite: false);

        project.Id.ShouldBe(SeedIds.Projects.Mob);
    }
}
