using System.Text;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Side;

[TestFixture]
public class WorkItemAttachmentUploadTests : SeededAcceptanceTest
{
    [Test]
    public async Task WorkItem_WhenAttachmentUploaded_ShouldDownloadSameBytes()
    {
        var screen = new WorkItemScreen(Page);
        var side = new SideRailPanel(Page);
        await screen.OpenAsync("SHIP", "SHIP-147");
        var bytes = Encoding.UTF8.GetBytes("vault api contract, v3\n");
        await Expect(side.AttachmentCount).ToHaveTextAsync("0");

        var response = await side.UploadAsync(SeedIds.WorkItems.Ship147, "contract.txt", bytes, "text/plain");

        response.Ok.ShouldBeTrue();
        await Expect(side.Attachments).ToHaveCountAsync(1);
        await Expect(side.Downloads).ToHaveTextAsync("contract.txt");
        var href = await side.Downloads.GetAttributeAsync("href");
        var download = await Page.Context.APIRequest.GetAsync(href!);
        download.Ok.ShouldBeTrue();
        (await download.BodyAsync()).ShouldBe(bytes);
        download.Headers["x-content-type-options"].ShouldBe("nosniff");
        download.Headers["content-security-policy"].ShouldBe("sandbox; frame-ancestors 'none'");
        download.Headers["content-disposition"].ShouldStartWith("attachment");
        await Page.ReloadAsync();
        await screen.Screen.WaitForAsync();
        await Expect(side.Attachments).ToHaveCountAsync(1);
    }
}
