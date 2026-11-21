using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Prompts;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Prompts;
using ShipLean.TestSupport.TestIds;
using ShipLean.WebApi.Features.Prompts;

namespace ShipLean.AcceptanceTests.Lanes.Prompts;

// SHIP-744 AC2: Priya is a member of SHIP, not an admin. She reads every prompt and every tab, and nothing offers a change.
// Brief has v1 (not in use) and v2 in use, so an admin would see "Use v1" in History. Report has a pending built-in update,
// so an admin would see Review, Apply and Keep.
[TestFixture]
public class MemberJourneyTests : SeededAcceptanceTest
{
    protected override TestUser SignedInAs => TestUser.Priya;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddSingleton<IBuiltInPrompts>(new ChangedBuiltInPrompts(new BuiltInPrompts(), new Dictionary<PromptKey, string> { [PromptKey.Report] = "A newer report built-in." }));

    [OneTimeSetUp]
    public async Task ArrangeCustomPromptsAsync()
    {
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Brief)
            .WithNumber(1).WithText("# Brief\n\nFirst wording.").WithNote("First wording").WithCreatedBy(SeedIds.Users.Joel)
            .InsertAsync(ConnectionString);
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Brief)
            .WithNumber(2).WithText("# Brief\n\nBuild {{key}} the way the team does.").WithNote("Team wording").WithCreatedBy(SeedIds.Users.Joel)
            .InUse().InsertAsync(ConnectionString);
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Report)
            .WithText("Mine.").WithCreatedBy(SeedIds.Users.Joel).InUse().InsertAsync(ConnectionString);
    }

    [Test]
    public async Task PromptsTab_WhenMember_ShouldReadEveryPromptWithoutActions()
    {
        var prompts = new PromptsMemberPage(Page);
        await prompts.OpenAsync("SHIP", "brief");

        await Expect(prompts.Rows).ToHaveCountAsync(11);
        await Expect(prompts.DetailName).ToContainTextAsync("brief");
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v2");
        await Expect(prompts.ReadOnlyNote).ToBeVisibleAsync();
        await Expect(prompts.ReadOnlyAdmins).ToContainTextAsync("Joel Dickson");

        await WalkTabsAsync(prompts);
        await prompts.Tab(PromptsTestIds.TabHistory).ClickAsync();
        await Expect(prompts.HistoryRowInUse).ToContainTextAsync("Team wording");
        await Expect(prompts.PanelHistory).ToContainTextAsync("First wording");

        await prompts.OpenAsync("SHIP", "report");
        await Expect(prompts.DetailChip).ToContainTextAsync("Built-in updated");
        await Expect(prompts.Tab(PromptsTestIds.TabUpdate)).ToBeVisibleAsync();
        await WalkTabsAsync(prompts);
        await prompts.Tab(PromptsTestIds.TabUpdate).ClickAsync();
        await Expect(prompts.Panel(PromptsTestIds.PanelUpdate)).ToBeVisibleAsync();
        await ExpectNoChangeActionsAsync(prompts);
    }

    [Test]
    public async Task PromptsTab_WhenMemberPostsAWrite_ShouldBeForbidden()
    {
        var prompts = new PromptsMemberPage(Page);
        await prompts.OpenAsync("SHIP", "brief");

        var response = await Page.Context.APIRequest.PostAsync(
            $"{BaseUrl}/api/projects/{SeedIds.Projects.Ship}/prompts/brief/use-built-in",
            new() { Headers = new Dictionary<string, string> { [ShipLean.Contracts.ClientHeader.Name] = "acceptance-test" } });

        response.Status.ShouldBe(403);
        using var body = JsonDocument.Parse(await response.TextAsync());
        body.RootElement.GetProperty("code").GetString().ShouldBe(ShipLean.Contracts.Errors.ApiErrorCodes.Forbidden);
    }

    private static async Task WalkTabsAsync(PromptsMemberPage prompts)
    {
        string[] tabs =
        [
            PromptsTestIds.TabSource, PromptsTestIds.TabRendered, PromptsTestIds.TabYourChanges,
            PromptsTestIds.TabHistory, PromptsTestIds.TabAssembled,
        ];
        string[] panels =
        [
            PromptsTestIds.PanelSource, PromptsTestIds.PanelRendered, PromptsTestIds.PanelYourChanges,
            PromptsTestIds.PanelHistory, PromptsTestIds.PanelAssembled,
        ];
        for (var i = 0; i < tabs.Length; i++)
        {
            await prompts.Tab(tabs[i]).ClickAsync();
            await Expect(prompts.Panel(panels[i])).ToBeVisibleAsync();
            await ExpectNoChangeActionsAsync(prompts);
        }
    }

    private static async Task ExpectNoChangeActionsAsync(PromptsMemberPage prompts)
    {
        await Expect(prompts.Customize).ToHaveCountAsync(0);
        await Expect(prompts.Edit).ToHaveCountAsync(0);
        await Expect(prompts.UseBuiltIn).ToHaveCountAsync(0);
        await Expect(prompts.Resume).ToHaveCountAsync(0);
        await Expect(prompts.HistoryUse).ToHaveCountAsync(0);
        await Expect(prompts.HistoryBuiltInUse).ToHaveCountAsync(0);
        await Expect(prompts.UpdateReview).ToHaveCountAsync(0);
        await Expect(prompts.UpdateApply).ToHaveCountAsync(0);
        await Expect(prompts.UpdateKeep).ToHaveCountAsync(0);
    }
}
