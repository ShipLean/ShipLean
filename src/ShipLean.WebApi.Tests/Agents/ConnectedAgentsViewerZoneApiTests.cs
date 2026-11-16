using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Agents;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;

namespace ShipLean.WebApi.Tests.Agents;

// The Connected agents label in the viewer's zone, through the API. Its own class so the app's clock, which these tests set, stays out of the other agent tests.
[TestFixture]
public class ConnectedAgentsViewerZoneApiTests : ApiFixture
{
    private readonly SettableClock _clock = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        PinnedAgentFakes.Pin(services);
        services.AddSingleton<TimeProvider>(_clock);
    };

    // The connection is made at LateEveningUtc; the label is read long after, so it is a short day with its year.
    private async Task<string> ConnectedLabelAsync(string? zone)
    {
        TestUser viewer;
        await using (var connection = await OpenDatabaseAsync())
        {
            viewer = TestUser.Of(await new AppUserBuilder().InsertAsync(connection));
        }

        if (zone is not null)
        {
            await ViewerZoneArrange.SetAsync(Api, viewer, zone);
        }

        _clock.Now = ViewerZoneTestData.LateEveningUtc;
        await Host.Services.GetRequiredService<IAgentAuthorizations>().CreateAsync(
            viewer.Id!.Value, InMemoryAgentStore.ClaudeCode.ClientId, new HashSet<string> { "shiplean:read" });
        _clock.Now = ViewerZoneTestData.LateEveningUtc.AddDays(800);

        return (await Api.ActingAs(viewer).GetJsonAsync<List<ConnectedAgentDto>>("api/me/agents")).ShouldHaveSingleItem().ConnectedLabel;
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to the ConnectedLabel Relative call in InMemoryAgentAuthorizations.ListAsync.
    [Test]
    public async Task ConnectedAgents_WhenViewerIsInBangkok_ShouldDateAConnectionByTheBangkokDay()
    {
        var label = await ConnectedLabelAsync(ViewerZoneTestData.BangkokId);

        // 20:00 UTC is 03:00 the next day in Bangkok.
        var bangkokDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.FromHours(7)).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        var utcDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        bangkokDay.ShouldNotBe(utcDay);
        label.ShouldBe(bangkokDay);
    }

    [Test]
    public async Task ConnectedAgents_WhenViewerHasNoZone_ShouldDateAConnectionByTheUtcDay()
    {
        var label = await ConnectedLabelAsync(null);

        label.ShouldBe(ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture));
    }

    private sealed class SettableClock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = ViewerZoneTestData.LateEveningUtc;

        public override DateTimeOffset GetUtcNow() => Now;
    }
}
