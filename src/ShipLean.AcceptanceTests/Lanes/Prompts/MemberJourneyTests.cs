using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Prompts;
using ShipLean.Contracts.Prompts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Prompts;
using ShipLean.TestSupport.TestIds;

namespace ShipLean.AcceptanceTests.Lanes.Prompts;

// SHIP-744 AC2: Priya is a member of SHIP, not an admin. She reads every prompt and every tab, and nothing offers a change.
[TestFixture]
public class MemberJourneyTests : SeededAcceptanceTest
{
    protected override TestUser SignedInAs => TestUser.Priya;

    [OneTimeSetUp]
    public async Task ArrangeACustomBriefAsync()
    {
        await new PromptVersionBuilder(SeedIds.Projects.Ship, PromptKey.Brief)
            .WithText("# Brief\n\nBuild {{key}} the way the team does.")
            .WithNote("Team wording")
            .WithCreatedBy(SeedIds.Users.Joel)
            .InUse()
            .InsertAsync(ConnectionString);
    }

    [Test]
    public async Task PromptsTab_WhenMember_ShouldReadEveryPromptWithoutActions()
    {
        var prompts = new PromptsMemberPage(Page);
        await prompts.OpenAsync("SHIP", "brief");

        await Expect(prompts.Rows).ToHaveCountAsync(11);
        await Expect(prompts.DetailName).ToContainTextAsync("brief");
        await Expect(prompts.DetailChip).ToContainTextAsync("Custom v1");
        await Expect(prompts.ReadOnlyNote).ToBeVisibleAsync();
        await Expect(prompts.ReadOnlyAdmins).ToContainTextAsync("Joel Dickson");

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

    [Test]
    public async Task PromptsTab_WhenMemberPostsAWrite_ShouldBeForbidden()
    {
        var prompts = new PromptsMemberPage(Page);
        await prompts.OpenAsync("SHIP", "brief");

        var response = await Page.Context.APIRequest.PostAsync(
            $"{BaseUrl}/api/projects/{SeedIds.Projects.Ship}/prompts/brief/use-built-in",
            new() { Headers = new Dictionary<string, string> { [ShipLean.Contracts.ClientHeader.Name] = "acceptance-test" } });

        response.Status.ShouldBe(403);
    }

    private static async Task ExpectNoChangeActionsAsync(PromptsMemberPage prompts)
    {
        await Expect(prompts.Customize).ToHaveCountAsync(0);
        await Expect(prompts.Edit).ToHaveCountAsync(0);
        await Expect(prompts.UseBuiltIn).ToHaveCountAsync(0);
        await Expect(prompts.HistoryUse).ToHaveCountAsync(0);
        await Expect(prompts.HistoryBuiltInUse).ToHaveCountAsync(0);
        await Expect(prompts.UpdateApply).ToHaveCountAsync(0);
        await Expect(prompts.UpdateKeep).ToHaveCountAsync(0);
    }
}
