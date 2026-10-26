using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Instance;

// Mei is a project member, not an instance admin.
[TestFixture]
public class InstanceNotAdminTests : SeededAcceptanceTest
{
    protected override TestUser SignedInAs => TestUser.Mei;

    [Test]
    public async Task Instance_WhenNotInstanceAdmin_ShouldBeForbidden()
    {
        var general = new InstanceGeneralPage(Page);
        await general.OpenAsync();

        await Expect(general.Denied).ToBeVisibleAsync();
        await Expect(general.BaseUrl).ToHaveCountAsync(0);
        var read = await Page.Context.APIRequest.GetAsync("/api/instance/projects");
        read.Status.ShouldBe(403);
    }
}
