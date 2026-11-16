using System.Globalization;
using Dapper;
using ShipLean.Contracts.Users.Admin;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;

namespace ShipLean.WebApi.Tests.Users.Admin;

// The Users tab's last sign-in label in the viewer's zone. Its own class so the zone it sets on the instance admin stays out of the other Users tests.
[TestFixture]
public class UsersAdminViewerZoneApiTests : ApiFixture
{
    private async Task<string?> LastSignInLabelAsync()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE app_user SET last_sign_in_at = @at WHERE id = @id", new { at = ViewerZoneTestData.LateEveningUtc, id = SeedIds.Users.Tom });
        }

        var users = await Api.ActingAs(TestUser.Joel).GetJsonAsync<AdminUsersDto>("api/admin/users");

        return users.Users.Single(u => u.Id == SeedIds.Users.Tom).LastSignIn;
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to the Relative call in UserAdmin.
    [Test]
    public async Task UserAdmin_WhenViewerIsInBangkok_ShouldDateTheLastSignInByTheBangkokDay()
    {
        await ViewerZoneArrange.SetAsync(Api, TestUser.Joel, ViewerZoneTestData.BangkokId);

        var label = await LastSignInLabelAsync();

        // 20:00 UTC is 03:00 the next day in Bangkok; two years old, so the label is the short day with its year.
        var bangkokDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.FromHours(7)).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        var utcDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture);
        bangkokDay.ShouldNotBe(utcDay);
        label.ShouldBe(bangkokDay);
    }

    [Test]
    public async Task UserAdmin_WhenViewerHasNoZone_ShouldDateTheLastSignInByTheUtcDay()
    {
        var label = await LastSignInLabelAsync();

        label.ShouldBe(ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.Zero).ToString("d MMM yyyy", CultureInfo.InvariantCulture));
    }
}
