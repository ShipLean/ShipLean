using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Settings.NewProject;

// Joel creates a project from the defaults, with a member, and lands on its empty Stories backlog.
[TestFixture]
public class NewProjectCreateTests : SeededAcceptanceTest
{
    [Test]
    public async Task NewProject_WhenCreated_ShouldOpenItsBacklogRoute()
    {
        var form = new NewProjectPage(Page);
        await form.OpenAsync();

        await form.Name.FillAsync("Customer Support Hub");
        await Expect(form.Key).ToHaveValueAsync("CSH");
        await Expect(form.KeyFree).ToHaveAttributeAsync("data-ok", "true");
        await Page.GetByTestId(SettingsTestIds.NewProjectMemberEmail).FillAsync("priya@shiplean.local");
        await Page.GetByTestId(SettingsTestIds.NewProjectMemberAdd).ClickAsync();
        await Expect(Page.GetByTestId(SettingsTestIds.NewProjectMemberRow)).ToContainTextAsync("Priya");
        await form.SprintAsync("2026-10-26", "2026-11-06");
        await Expect(form.CreateButton).ToHaveTextAsync("Create CSH");

        var response = await form.CreateAsync();

        response.Ok.ShouldBeTrue();
        await Page.WaitForURLAsync(url => url.EndsWith(Links.Backlog("CSH")));
        var project = await Page.APIRequest.GetAsync("/api/projects/by-key/CSH");
        project.Ok.ShouldBeTrue();
        var members = await Page.APIRequest.GetAsync($"/api/projects/{(await project.JsonAsync()).GetValueOrDefault().GetProperty("id").GetGuid()}/members");
        (await members.TextAsync()).ShouldContain("priya@shiplean.local");
    }
}
