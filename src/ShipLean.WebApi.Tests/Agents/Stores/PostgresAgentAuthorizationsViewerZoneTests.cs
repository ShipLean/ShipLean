using System.Globalization;
using ShipLean.Application.Features.Agents.Stores;
using ShipLean.TestSupport.Identity;

namespace ShipLean.WebApi.Tests.Agents.Stores;

// The Connected agents labels of the real authorizations store, in the zone of the viewer it is given.
[TestFixture]
public sealed class PostgresAgentAuthorizationsViewerZoneTests
{
    private PostgresAgentDatabase _database = null!;

    [OneTimeSetUp]
    public async Task CloneDatabase() => _database = await PostgresAgentDatabase.CreateAsync();

    [OneTimeTearDown]
    public async Task DropDatabase() => await _database.DisposeAsync();

    private async Task<string> ConnectedLabelAsync(FixedViewerZone viewer)
    {
        var world = _database.NewWorld();
        var userId = await world.ArrangeUserAsync();
        var id = await world.Authorizations.CreateAsync(userId, await world.RegisterClientAsync(), new HashSet<string> { "shiplean:read" });
        await _database.ExecuteAsync("UPDATE oauth_authorization SET created_at = @at WHERE id = @id", new { at = ViewerZoneTestData.LateEveningUtc, id });
        var store = new AgentAuthorizations(_database.DataSource, world.Clock, viewer);

        return (await store.ListAsync(userId)).Single().ConnectedLabel;
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to the ConnectedLabel Relative call in AgentAuthorizations.
    [Test]
    public async Task ConnectedAgents_WhenViewerIsInBangkok_ShouldDateAConnectionByTheBangkokDay()
    {
        var label = await ConnectedLabelAsync(new FixedViewerZone(ViewerZoneTestData.Bangkok));

        // 20:00 UTC is 03:00 the next day in Bangkok; more than a year old, so the label is the short day with its year.
        var bangkokDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.FromHours(7)).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        var utcDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        bangkokDay.ShouldNotBe(utcDay);
        label.ShouldBe(bangkokDay);
    }

    [Test]
    public async Task ConnectedAgents_WhenViewerHasNoZone_ShouldDateAConnectionByTheUtcDay()
    {
        var label = await ConnectedLabelAsync(new FixedViewerZone());

        label.ShouldBe(ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture));
    }
}
