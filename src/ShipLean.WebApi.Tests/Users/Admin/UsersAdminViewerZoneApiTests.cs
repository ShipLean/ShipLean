using System.Globalization;
using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Users.Admin;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Users.Admin;

// The Users tab's last sign-in label in the viewer's zone. Its own class so the zone it sets on the instance admin stays out of the other Users tests.
[TestFixture]
public class UsersAdminViewerZoneApiTests : ApiFixture
{
    // A viewer of its own, an instance admin: the seeded users' zones are never touched.
    private async Task<TestUser> ArrangeViewerAsync(string? zone)
    {
        await using var connection = await OpenDatabaseAsync();
        var viewer = TestUser.Of(await new AppUserBuilder().Admin().InsertAsync(connection));
        if (zone is not null)
        {
            await ViewerZoneArrange.SetAsync(Api, viewer, zone);
        }

        return viewer;
    }

    private async Task<string?> LastSignInLabelAsync(TestUser viewer)
    {
        Guid person;
        await using (var connection = await OpenDatabaseAsync())
        {
            person = await new AppUserBuilder().InsertAsync(connection);
            await connection.ExecuteAsync("UPDATE app_user SET last_sign_in_at = @at WHERE id = @id", new { at = ViewerZoneTestData.LateEveningUtc, id = person });
        }

        using var response = await Api.ActingAs(viewer).GetAsync("api/admin/users");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var users = JsonSerializer.Deserialize<AdminUsersDto>(await response.Content.ReadAsStringAsync(), AuthClient.Json)!;

        return users.Users.Single(u => u.Id == person).LastSignIn;
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to the Relative call in UserAdmin.
    [Test]
    public async Task UserAdmin_WhenViewerIsInBangkok_ShouldDateTheLastSignInByTheBangkokDay()
    {
        var viewer = await ArrangeViewerAsync(ViewerZoneTestData.BangkokId);

        var label = await LastSignInLabelAsync(viewer);

        // 20:00 UTC is 03:00 the next day in Bangkok; two years old, so the label is the short day with its year.
        var bangkokDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.FromHours(7)).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        var utcDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        bangkokDay.ShouldNotBe(utcDay);
        label.ShouldBe(bangkokDay);
    }

    [Test]
    public async Task UserAdmin_WhenViewerHasNoZone_ShouldDateTheLastSignInByTheUtcDay()
    {
        var viewer = await ArrangeViewerAsync(null);

        var label = await LastSignInLabelAsync(viewer);

        label.ShouldBe(ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture));
    }
}
