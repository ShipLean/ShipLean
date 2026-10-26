using System.Text.Json;
using System.Threading.Channels;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Application.Fakes.Comments;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Work;

// The Do-the-work tools as Joel's agent, over /mcp, against the real stores (comments are the in-memory fake). Every test
// arranges its own items in SHIP; the sprint is the one running today, found by the tools as "current".
[TestFixture]
public class WorkToolsTests : McpFixture
{
    private const string AgentName = "Test Agent (unverified)";

    private Guid _sprintId;
    private string _bearer = string.Empty;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddSingleton<StoreInterference>();
        services.RemoveAll<ICommentStore>();
        services.AddSingleton<InMemoryCommentStore>();
        services.AddSingleton<ICommentStore>(sp => sp.GetRequiredService<InMemoryCommentStore>());
        WorkItemStoreDecoration.Decorate(services, (real, sp) => new InterferingWorkItemStore(real, sp.GetRequiredService<StoreInterference>()));
    };

    private StoreInterference Interference => Host.Services.GetRequiredService<StoreInterference>();

    [OneTimeSetUp]
    public async Task ArrangeASprintAndAToken()
    {
        _sprintId = Guid.NewGuid();
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @project, 'Agent Work Sprint', DATE '2000-01-01', DATE '2099-12-31')",
            new { id = _sprintId, project = SeedIds.Projects.Ship });
        _bearer = await TokenAsync(TestUser.Joel, ReadWrite);
    }

    [SetUp]
    public void ResetInterference() => Interference.Reset();

    [Test]
    public async Task FindWork_ShouldListUnassignedProposedInStackOrder()
    {
        var parent = await ArrangeAsync(b => b.WithType("story").WithTitle("Find parent").WithSprint(_sprintId));
        var task = await ArrangeAsync(b => b.WithType("task").WithTitle("Find task").WithSprint(_sprintId).WithParent(parent));
        await ArrangeAsync(b => b.WithType("story").WithTitle("Find taken").WithSprint(_sprintId).WithAssignee(SeedIds.Users.Priya));
        await ArrangeAsync(b => b.WithType("story").WithTitle("Find started").WithSprint(_sprintId).WithStateNamed("Active"));
        await ArrangeAsync(b => b.WithType("story").WithTitle("Find unplanned"));
        var all = await CallAsync("find_work_to_pick_up", """{"project":"SHIP"}""");
        var stories = await CallAsync("find_work_to_pick_up", """{"project":"SHIP","type":"story","sprint":"Agent Work Sprint"}""");
        var under = await CallAsync("find_work_to_pick_up", $$"""{"project":"SHIP","parent":"{{await KeyOfAsync(parent)}}"}""");

        Keys(all).ShouldBe([await KeyOfAsync(parent), await KeyOfAsync(task)]);
        all.GetProperty("sprint").GetString().ShouldBe("Agent Work Sprint");
        Keys(stories).ShouldBe([await KeyOfAsync(parent)]);
        Keys(under).ShouldBe([await KeyOfAsync(task)]);
        all.GetProperty("project").GetString().ShouldBe("SHIP");
        all.GetProperty("open_url").GetString()!.ShouldContain("/p/SHIP/");
    }

    [Test]
    public async Task PickUp_ShouldAssignMeAndStartFirstInProgress()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Pick me").WithSprint(_sprintId));
        var key = await KeyOfAsync(story);

        var result = await CallAsync("pick_up", $$"""{"item":"{{key}}"}""");

        result.GetProperty("state").GetString().ShouldBe("Active");
        result.GetProperty("assigned_to_you").GetBoolean().ShouldBeTrue();
        result.GetProperty("open_url").GetString().ShouldEndWith($"/p/SHIP/items/{key}");
        var item = await GetAsync(story);
        item.StateId.ShouldBe(SeedIds.States.Ship.StoryActive);
        item.AssigneeId.ShouldBe(SeedIds.Users.Joel);
    }

    [Test]
    public async Task PickUp_WhenAssignedToSomeoneElse_ShouldBeConflict()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Taken").WithSprint(_sprintId).WithAssignee(SeedIds.Users.Priya));

        var reply = await CallToolAsync(_bearer, "pick_up", $$"""{"item":"{{await KeyOfAsync(story)}}"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Conflict);
        (await GetAsync(story)).AssigneeId.ShouldBe(SeedIds.Users.Priya);
    }

    [Test]
    public async Task UpdateProgress_WhenLastTaskClosed_ShouldOfferParentResolved()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Parent").WithStateNamed("Active"));
        await ArrangeAsync(b => b.WithType("task").WithTitle("Done already").WithParent(story).WithStateNamed("Closed"));
        var last = await ArrangeAsync(b => b.WithType("task").WithTitle("Last open").WithParent(story).WithStateNamed("Active").WithAssignee(SeedIds.Users.Joel));

        var result = await CallAsync("update_progress", $$"""{"item":"{{await KeyOfAsync(last)}}","state":"Closed","note":"Merged.","link_url":"https://example.test/pr/1","link_title":"PR 1","points":2}""");

        result.GetProperty("state").GetString().ShouldBe("Closed");
        result.GetProperty("last_task_closed").GetBoolean().ShouldBeTrue();
        result.GetProperty("parent_key").GetString().ShouldBe(await KeyOfAsync(story));
        result.GetProperty("move_parent_to").GetString().ShouldBe("Resolved");
        result.GetProperty("note_added").GetBoolean().ShouldBeTrue();
        result.GetProperty("link_added").GetBoolean().ShouldBeTrue();
        result.GetProperty("points").GetDecimal().ShouldBe(2);
        (await GetAsync(story)).StateId.ShouldBe(SeedIds.States.Ship.StoryActive);
        (await GetAsync(last)).Links.ShouldContain(l => l.Value.Url == "https://example.test/pr/1");
    }

    [Test]
    public async Task UpdateProgress_WhenOtherTasksStillOpen_ShouldNotOfferParent()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Parent two").WithStateNamed("Active"));
        await ArrangeAsync(b => b.WithType("task").WithTitle("Still open").WithParent(story).WithStateNamed("Active"));
        var closing = await ArrangeAsync(b => b.WithType("task").WithTitle("Closing").WithParent(story).WithStateNamed("Active"));

        var result = await CallAsync("update_progress", $$"""{"item":"{{await KeyOfAsync(closing)}}","state":"Closed"}""");

        result.GetProperty("last_task_closed").GetBoolean().ShouldBeFalse();
        result.GetProperty("move_parent_to").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task UpdateProgress_WhenStaleDisjoint_ShouldRetryOnce()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Disjoint").WithSprint(_sprintId));
        Interference.Before(new WorkItemChanges { Title = new FieldSet<string>("Retitled by a person") });

        var result = await CallAsync("update_progress", $$"""{"item":"{{await KeyOfAsync(story)}}","points":5}""");

        Interference.PatchCalls.ShouldBe(2);
        result.GetProperty("points").GetDecimal().ShouldBe(5);
        var item = await GetAsync(story);
        item.Points.ShouldBe(5);
        item.Title.ShouldBe("Retitled by a person");
    }

    [Test]
    public async Task UpdateProgress_WhenStaleSameField_ShouldReportConflict()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Same field").WithSprint(_sprintId));
        Interference.Before(new WorkItemChanges { Points = new FieldSet<decimal?>(8) });

        var reply = await CallToolAsync(_bearer, "update_progress", $$"""{"item":"{{await KeyOfAsync(story)}}","points":5}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Conflict);
        reply.Structured().GetProperty("error").GetProperty("message").GetString()!.ShouldContain("points");
        Interference.PatchCalls.ShouldBe(1);
        (await GetAsync(story)).Points.ShouldBe(8);
    }

    [Test]
    public async Task UpdateProgress_WhenStateIsAnotherTypesOnly_ShouldBeAmbiguousWithCandidates()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Wrong state").WithSprint(_sprintId));

        var reply = await CallToolAsync(_bearer, "update_progress", $$"""{"item":"{{await KeyOfAsync(story)}}","state":"In review"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Ambiguous);
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("field").GetString().ShouldBe("state");
        error.GetProperty("candidates").EnumerateArray().Select(c => c.GetString()).ShouldContain("Active");
    }

    [Test]
    public async Task UpdateProgress_WhenNoteFails_ShouldKeepTheChangeAndSayTheNoteDidNotLand()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Note fails").WithSprint(_sprintId), knownToComments: false);

        var result = await CallAsync("update_progress", $$"""{"item":"{{await KeyOfAsync(story)}}","points":3,"note":"Will not land."}""");

        result.GetProperty("note_added").GetBoolean().ShouldBeFalse();
        result.GetProperty("note_error").GetString().ShouldNotBeNullOrEmpty();
        (await GetAsync(story)).Points.ShouldBe(3);
    }

    [Test]
    public async Task UpdateProgress_WhenNothingToUpdate_ShouldBeValidation()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Nothing").WithSprint(_sprintId));

        var reply = await CallToolAsync(_bearer, "update_progress", $$"""{"item":"{{await KeyOfAsync(story)}}"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task UpdateProgress_WhenItemForeignAndNothingToUpdate_ShouldBeNotFound()
    {
        var reply = await CallToolAsync(_bearer, "update_progress", """{"item":"PART-1"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task FindWork_WhenTypeIsANumber_ShouldBeValidation()
    {
        var reply = await CallToolAsync(_bearer, "find_work_to_pick_up", """{"project":"SHIP","type":"3"}""");

        ErrorCode(reply).ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task HandBack_ShouldUnassignAndReturnToFirstProposed()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Hand me back").WithSprint(_sprintId).WithStateNamed("Active").WithAssignee(SeedIds.Users.Joel));

        var result = await CallAsync("hand_back", $$"""{"item":"{{await KeyOfAsync(story)}}","note":"Blocked on the API."}""");

        result.GetProperty("state").GetString().ShouldBe("New");
        result.GetProperty("note_added").GetBoolean().ShouldBeTrue();
        var item = await GetAsync(story);
        item.StateId.ShouldBe(SeedIds.States.Ship.StoryNew);
        item.AssigneeId.ShouldBeNull();
        var comment = (await Host.Services.GetRequiredService<ICommentStore>().ListAsync(story)).ShouldHaveSingleItem();
        comment.Body.ShouldBe("Blocked on the API.");
        comment.AgentName.ShouldBe(AgentName);
    }

    [Test]
    public async Task Write_ShouldPublishEventForOpenBoards()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Live board").WithSprint(_sprintId));
        var published = Channel.CreateUnbounded<WorkItemUpdated>();
        using var subscription = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(SeedIds.Projects.Ship, e =>
        {
            if (e is WorkItemUpdated updated && updated.Id == story)
            {
                published.Writer.TryWrite(updated);
            }

            return Task.CompletedTask;
        });

        await CallAsync("pick_up", $$"""{"item":"{{await KeyOfAsync(story)}}"}""");

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var seen = await published.Reader.ReadAsync(timeout.Token);
        seen.Fields.ShouldContain("state");
        seen.Fields.ShouldContain("assignee");
    }

    [Test]
    public async Task Write_ShouldRecordAgentName()
    {
        var story = await ArrangeAsync(b => b.WithType("story").WithTitle("Via agent").WithSprint(_sprintId));

        await CallAsync("pick_up", $$"""{"item":"{{await KeyOfAsync(story)}}"}""");

        using var scope = Host.Services.CreateScope();
        var revisions = await scope.ServiceProvider.GetRequiredService<IWorkItemStore>().GetRevisionsAsync(story);
        revisions[^1].AgentName.ShouldBe(AgentName);
    }

    private async Task<JsonElement> CallAsync(string tool, string arguments)
    {
        var reply = await CallToolAsync(_bearer, tool, arguments);
        reply.IsToolError().ShouldBeFalse(reply.Body);
        return reply.Structured();
    }

    private static string? ErrorCode(McpReply reply)
    {
        reply.IsToolError().ShouldBeTrue(reply.Body);
        return reply.Structured().GetProperty("error").GetProperty("code").GetString();
    }

    private static string[] Keys(JsonElement result)
        => [.. result.GetProperty("items").EnumerateArray().Select(i => i.GetProperty("key").GetString()!)];

    // The comment double only knows the items it is told about; an item it was not told about is how a note fails.
    private async Task<Guid> ArrangeAsync(Func<WorkItemBuilder, WorkItemBuilder> configure, bool knownToComments = true)
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await configure(new WorkItemBuilder(SeedIds.Projects.Ship)).InsertAsync(connection);
        if (knownToComments)
        {
            Host.Services.GetRequiredService<InMemoryCommentStore>().AddWorkItem(id);
        }

        return id;
    }

    private async Task<string> KeyOfAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
    }

    private async Task<WorkItemSnapshot> GetAsync(Guid id)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IWorkItemStore>().GetAsync(id);
    }
}
