using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Comments;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Capture;

// refine_work_item, comment and link on items arranged in SHIP, as Joel; one item in PART for the foreign target.
[TestFixture]
public class ItemToolsTests : McpFixture
{
    // Known before the host starts, so the comment double can be told about them.
    private static readonly Guid _story = new("00000009-0000-4000-8001-0000000000a1");
    private string _storyKey = string.Empty;
    private string _foreignKey = string.Empty;

    protected override bool WithProbeTools => false;

    // ICommentStore is still the in-memory fake, which only knows the work items it is told about: pin one that knows the
    // arranged story.
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices?.Invoke(services);
        services.AddSingleton<ICommentStore>(provider =>
        {
            var comments = ActivatorUtilities.CreateInstance<InMemoryCommentStore>(provider);
            comments.AddWorkItem(_story);
            return comments;
        });
    };

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        await using var connection = await OpenDatabaseAsync();
        await new WorkItemBuilder(SeedIds.Projects.Ship).WithId(_story).WithType("story").WithTitle("Refine me").InsertAsync(connection);
        _storyKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @_story", new { _story });
        var foreign = await new WorkItemBuilder(SeedIds.Projects.Part).WithTitle("Foreign").InsertAsync(connection);
        _foreignKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @foreign", new { foreign });
    }

    private async Task<WorkItemSnapshot> PatchAsync(Guid id, WorkItemChanges changes)
    {
        Host.ActAs(TestUser.Joel.Id);
        try
        {
            using var scope = Host.Services.CreateScope();
            var store = scope.ServiceProvider.GetRequiredService<IWorkItemStore>();
            var current = await store.GetAsync(id);
            var outcome = await store.PatchAsync(id, new WorkItemPatch(current.Revision, changes), WriteOptions.User);
            outcome.Written.ShouldNotBeNull();
            return await store.GetAsync(id);
        }
        finally
        {
            Host.ActAs(null);
        }
    }

    [Test]
    public async Task Refine_WhenChecklistTextUnchanged_ShouldKeepDone()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        await PatchAsync(_story, new WorkItemChanges { AcceptanceCriteria = new FieldSet<IReadOnlyList<ChecklistItem>>([new("Login works", true), new("Logout works", false)]) });

        var reply = await CallToolAsync(token, "refine_work_item", $$"""{"item":"{{_storyKey}}","acceptance_criteria":["Logout works","Login works","Reset works"],"points":5,"note":"Added the reset case."}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("note_added").GetBoolean().ShouldBeTrue();
        using var scope = Host.Services.CreateScope();
        var after = await scope.ServiceProvider.GetRequiredService<IWorkItemStore>().GetAsync(_story);
        after.AcceptanceCriteria.ShouldBe([new ChecklistItem("Logout works", false), new ChecklistItem("Login works", true), new ChecklistItem("Reset works", false)]);
        after.Points.ShouldBe(5m);
        (await Host.Services.GetRequiredService<ICommentStore>().ListAsync(_story)).Select(c => c.Body).ShouldContain("Added the reset case.");
    }

    [Test]
    public async Task Refine_WhenExpectedRevisionStale_ShouldBeConflict()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var read = await PatchAsync(_story, new WorkItemChanges { Priority = new FieldSet<int>(3) });

        var reply = await CallToolAsync(token, "refine_work_item", $$"""{"item":"{{_storyKey}}","title":"Stale title","expected_revision":{{read.Revision - 1}}}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Conflict);
        using var scope = Host.Services.CreateScope();
        (await scope.ServiceProvider.GetRequiredService<IWorkItemStore>().GetAsync(_story)).Title.ShouldBe("Refine me");
    }

    [Test]
    public async Task Comment_ShouldCarryAgentName()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "comment", $$"""{"item":"{{_storyKey}}","text":"Looks good to me."}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("agent_name").GetString().ShouldBe("Test Agent (unverified)");
        var comment = (await Host.Services.GetRequiredService<ICommentStore>().ListAsync(_story)).Single(c => c.Body == "Looks good to me.");
        comment.AgentName.ShouldBe("Test Agent (unverified)");
    }

    [Test]
    public async Task Link_WhenUrl_ShouldAddUrlLink()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "link", $$"""{"item":"{{_storyKey}}","url":"https://example.com/pull/7","title":"The pull request"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        using var scope = Host.Services.CreateScope();
        var links = (await scope.ServiceProvider.GetRequiredService<IWorkItemStore>().GetAsync(_story)).Links;
        links.ShouldContain(l => l.Value.Type == LinkType.Url && l.Value.Url == "https://example.com/pull/7" && l.Value.Title == "The pull request");
    }

    [Test]
    public async Task Link_WhenItemToItem_ShouldAddTypedLink()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "link", $$"""{"item":"{{_storyKey}}","to":"SHIP-90","type":"blocked_by"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        using var scope = Host.Services.CreateScope();
        var links = (await scope.ServiceProvider.GetRequiredService<IWorkItemStore>().GetAsync(_story)).Links;
        links.ShouldContain(l => l.Value.Type == LinkType.BlockedBy && l.Value.Key == "SHIP-90");
    }

    [Test]
    public async Task Link_WhenTargetForeign_ShouldBeNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "link", $$"""{"item":"{{_storyKey}}","to":"{{_foreignKey}}"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Link_WhenItemForeignAndNoTarget_ShouldBeNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "link", $$"""{"item":"{{_foreignKey}}"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }
}
