using ShipLean.AcceptanceTests.Harness;
using System.Text.Json;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Instance;

// Joining changes Legacy Billing's members, so this class has the seed to itself.
[TestFixture]
public class InstanceJoinTests : SeededAcceptanceTest
{
    [Test]
    public async Task Instance_WhenJoinedAsAdmin_ShouldAppearInMembersApi()
    {
        var projects = new InstanceProjectsPage(Page);
        await projects.OpenAsync();
        await Expect(projects.Banners).ToHaveCountAsync(1);

        await projects.JoinAsync("ARCH", SeedIds.Projects.Arch);

        await Expect(projects.Banners).ToHaveCountAsync(0);
        var members = await Page.Context.APIRequest.GetAsync($"/api/projects/{SeedIds.Projects.Arch}/members");
        members.Status.ShouldBe(200);
        var joel = JsonDocument.Parse(await members.TextAsync()).RootElement.EnumerateArray()
            .Single(m => m.GetProperty("user").GetProperty("id").GetGuid() == SeedIds.Users.Joel);
        joel.GetProperty("role").GetString().ShouldBe("admin");
    }
}
