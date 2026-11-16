using Microsoft.Playwright;
using ShipLean.Contracts;

namespace ShipLean.AcceptanceTests.Harness;

[TestFixture]
public class BangkokViewerHarnessTests : BangkokViewerTest
{
    [Test]
    public async Task BangkokViewer_WhenSignedIn_ShouldHaveBrowserAndUserInAsiaBangkok()
    {
        await Page.GotoAsync("/");

        var browserZone = await Page.EvaluateAsync<string>("() => Intl.DateTimeFormat().resolvedOptions().timeZone");
        var me = await Page.Context.APIRequest.GetAsync("/api/me", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { [ClientHeader.Name] = "acceptance-test" },
        });
        var body = await me.JsonAsync();

        browserZone.ShouldBe("Asia/Bangkok");
        me.Status.ShouldBe(200);
        body.ShouldNotBeNull();
        body.Value.GetProperty("timeZone").GetString().ShouldBe("Asia/Bangkok");
    }
}
