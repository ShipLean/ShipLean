using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.WorkItem;
using ShipLean.TestSupport;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.WorkItem.Form;

[TestFixture]
public class PasteImageTests : SeededAcceptanceTest
{
    [Test]
    public async Task PasteImage_WhenSaved_ShouldAttachFileAndKeepReference()
    {
        var screen = new WorkItemScreen(Page);
        await screen.OpenAsync("SHIP", "SHIP-101");
        await Page.GetByTestId(WorkItemTestIds.DescriptionEdit).ClickAsync();
        var input = Page.GetByTestId(WorkItemTestIds.DescriptionInput);
        await input.FillAsync("Flow: ");

        // Save asks while the draft's render is still pending, so the paste waits for the render of the pasted text.
        // The clipboard holds one unnamed PNG; the client names it.
        await Page.RunAndWaitForResponseAsync(
            async () =>
            {
                await input.EvaluateAsync(
                    """
                    element => {
                        const data = new DataTransfer();
                        data.items.add(new File([new Uint8Array([137, 80, 78, 71, 13, 10, 26, 10])], '', { type: 'image/png' }));
                        element.dispatchEvent(new ClipboardEvent('paste', { clipboardData: data, bubbles: true, cancelable: true }));
                    }
                    """);
                await Expect(input).ToHaveValueAsync("Flow: ![](pasted-1.png)");
            },
            response => response.Url.Contains("/api/markdown/render") && response.Request.PostData?.Contains("pasted-1.png") == true);

        await screen.SaveAsync(SeedIds.WorkItems.Ship101);

        var attachments = await GetJsonAsync($"/api/work-items/{SeedIds.WorkItems.Ship101}/attachments");
        attachments.EnumerateArray().Select(a => a.GetProperty("name").GetString()).ShouldContain("pasted-1.png");
        var item = await GetJsonAsync($"/api/projects/{SeedIds.Projects.Ship}/work-items/by-key/SHIP-101");
        item.GetProperty("description").GetString().ShouldNotBeNull().ShouldContain("![](pasted-1.png)");
    }

    private async Task<JsonElement> GetJsonAsync(string path)
    {
        var response = await Page.Context.APIRequest.GetAsync(path);
        response.Ok.ShouldBeTrue();
        using var json = JsonDocument.Parse(await response.TextAsync());
        return json.RootElement.Clone();
    }
}
