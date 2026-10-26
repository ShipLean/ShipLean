using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Markdown;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.Mcp;
using ShipLean.TestSupport.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

[TestFixture]
public class WorkItemStoreCreateManyTests : PostgresWorkItemStoreTestBase
{
    [Test]
    public async Task CreateMany_ShouldPublishOneEventPerItemAfterCommit()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var publishedInsideTransaction = -1;
        var options = new WriteOptions((_, _) =>
        {
            publishedInsideTransaction = Arrangement.Events.Published.Count(e => e.ProjectId == project.ProjectId);
            return Task.CompletedTask;
        }, RevisionOrigin.User);

        var outcomes = await store.CreateManyAsync([Create(project, "A"), Create(project, "B"), Create(project, "C")], options);

        publishedInsideTransaction.ShouldBe(0);
        var created = Arrangement.Events.Published.OfType<WorkItemCreated>().Where(e => e.ProjectId == project.ProjectId).ToList();
        created.Select(e => e.Id).ShouldBe(outcomes.Select(o => o.Written!.Id));
        created.Select(e => e.Key).ShouldBe(outcomes.Select(o => o.Written!.Key));
    }

    [Test]
    public async Task CreateMany_WhenOneInvalid_ShouldPublishNothing()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();

        await Should.ThrowAsync<ApiException>(() => store.CreateManyAsync([Create(project, "Fine"), Create(project, " ")], WriteOptions.User));

        Arrangement.Events.Published.Where(e => e.ProjectId == project.ProjectId).ShouldBeEmpty();
    }

    [Test]
    public async Task CreateMany_WhenAgentActive_ShouldStampEveryRevision()
    {
        var agent = new SettableCurrentAgent();
        agent.ActAs("Claude Code", "claude.ai", true);
        await using var dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(Arrangement.Database.ConnectionString));
        var store = new WorkItemStore(
            dataSource, new FixedCurrentUser(Actor), new ShipLean.Application.Fakes.WorkItems.PassThroughWorkItemRules(), Arrangement.Events, new MarkdownRenderer(), TimeProvider.System, agent);
        var project = await Arrangement.ArrangeProjectAsync();

        var outcomes = await store.CreateManyAsync([Create(project, "A"), Create(project, "B")], WriteOptions.User);

        var ids = outcomes.Select(o => o.Written!.Id).ToArray();
        await using var connection = await Arrangement.Database.OpenAsync();
        var names = (await connection.QueryAsync<string?>("SELECT agent_name FROM revision WHERE work_item_id = ANY(@ids)", new { ids })).ToList();
        names.ShouldBe(["Claude Code (claude.ai)", "Claude Code (claude.ai)"]);
    }

    private static WorkItemCreate Create(ArrangedProject project, string title) => new(project.ProjectId, WorkItemTypeId.Story, title, null, null, null);
}
