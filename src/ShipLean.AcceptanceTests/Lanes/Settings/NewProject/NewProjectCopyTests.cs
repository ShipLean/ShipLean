using System.Text.Json;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Settings.NewProject;

// Copying from Storefront carries its states and none of its work items.
[TestFixture]
public class NewProjectCopyTests : SeededAcceptanceTest
{
    [Test]
    public async Task NewProject_WhenCopiedFrom_ShouldCarryStatesNotItems()
    {
        var form = new NewProjectPage(Page);
        await form.OpenAsync();
        await form.Name.FillAsync("Storefront Twin");
        await form.CopyFromAsync(SeedIds.Projects.Ship);
        await form.SprintAsync("2026-10-26", "2026-11-06");
        await Expect(form.KeyFree).ToHaveAttributeAsync("data-ok", "true");

        var response = await form.CreateAsync();

        response.Ok.ShouldBeTrue();
        var created = (await response.JsonAsync()).GetValueOrDefault().GetProperty("id").GetGuid();
        var source = await StatesAsync(SeedIds.Projects.Ship);
        var copy = await StatesAsync(created);
        source.Sum(s => s.Items).ShouldBeGreaterThan(0);
        copy.Select(s => (s.Type, s.Name, s.Category)).ShouldBe(source.Select(s => (s.Type, s.Name, s.Category)));
        copy.Sum(s => s.Items).ShouldBe(0);
    }

    private async Task<(string Type, string Name, string Category, int Items)[]> StatesAsync(Guid projectId)
    {
        var response = await Page.APIRequest.GetAsync($"/api/projects/{projectId}/states");
        response.Ok.ShouldBeTrue();
        return [.. (await response.JsonAsync()).GetValueOrDefault().EnumerateArray().Select(s => (
            s.GetProperty("type").GetString()!,
            s.GetProperty("name").GetString()!,
            s.GetProperty("category").GetString()!,
            s.GetProperty("itemCount").GetInt32()))];
    }
}
