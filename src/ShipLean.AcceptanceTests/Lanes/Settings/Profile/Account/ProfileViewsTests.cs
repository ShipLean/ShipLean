using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Profile.Account;

// Joel has a saved view; Reset saved views forgets it.
[TestFixture]
public class ProfileViewsTests : SeededAcceptanceTest
{
    private static readonly Dictionary<string, string> ClientHeader = new() { ["X-ShipLean-Client"] = "views-test" };

    [Test]
    public async Task Profile_WhenViewsReset_ShouldClearViewPreferencesAndKeepTheRest()
    {
        await SaveAsync("view:backlog", "grouped");
        await SaveAsync("theme", "dark");
        await SaveAsync("density", "comfortable");
        await SaveAsync("recentProjects", new[] { SeedIds.Projects.Ship });

        var account = new ProfileAccountPage(Page);
        await account.OpenAsync();
        await account.ResetViewsAsync();

        (await Page.Context.APIRequest.GetAsync("/api/me/preferences/view:backlog")).Status.ShouldBe(204);
        foreach (var kept in new[] { "theme", "density", "recentProjects" })
        {
            (await Page.Context.APIRequest.GetAsync($"/api/me/preferences/{kept}")).Status.ShouldBe(200);
        }
    }

    private async Task SaveAsync(string key, object value)
    {
        var saved = await Page.Context.APIRequest.PutAsync($"/api/me/preferences/{key}", new APIRequestContextOptions { DataObject = value, Headers = ClientHeader });
        saved.Status.ShouldBe(204);
    }
}
