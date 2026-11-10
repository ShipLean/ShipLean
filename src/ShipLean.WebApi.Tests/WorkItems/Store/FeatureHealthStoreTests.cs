using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.WorkItems;
using ShipLean.TestSupport.Screens;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// A Feature's health columns on Postgres: the agent stamp the store writes, the schema's own Feature-only check, and the
// builder's arranged health read back raw.
[TestFixture]
public class FeatureHealthStoreTests : PostgresWorkItemStoreTestBase
{
    private NpgsqlDataSource _dataSource = null!;

    [OneTimeSetUp]
    public void OpenDataSource() => _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(Arrangement.Database.ConnectionString));

    [OneTimeTearDown]
    public async Task CloseDataSource() => await _dataSource.DisposeAsync();

    private WorkItemStore StoreAt(FakeTimeProvider clock, AgentIdentity agent)
        => new(_dataSource, new FixedCurrentUser(Actor), new PassThroughWorkItemRules(), Arrangement.Events, new MarkdownRenderer(), clock, agent);

    [Test]
    public async Task Patch_WhenAnAgentSetsHealth_ShouldStampTheAgentName()
    {
        var setAt = TestDates.Today.AddHours(9);
        var store = StoreAt(new FakeTimeProvider(setAt), new AgentIdentity("Claude Code", "claude.ai", true));
        var project = await Arrangement.ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");

        WorkItemContractSupport.Written(await store.PatchAsync(
            feature.Id, new WorkItemPatch(1, new WorkItemChanges { Health = new(FeatureHealth.Blocked) }), new WriteOptions(null, RevisionOrigin.User, Via: "set_status")));

        await using var connection = await Arrangement.Database.OpenAsync();
        (await ScreensRows.HealthAsync(connection, feature.Id)).ShouldBe(new ScreensRows.Health("blocked", setAt, Actor, "Claude Code (claude.ai)"));
        var set = (await Arrangement.CreateReads().GetAsync(feature.Id)).HealthSet.ShouldNotBeNull();
        set.Agent.ShouldBe("Claude Code (claude.ai)");
        set.By.ShouldNotBeNull().Id.ShouldBe(Actor);
    }

    [Test]
    public async Task Create_WhenFeatureHasHealth_ShouldStampItAtCreation()
    {
        var setAt = TestDates.Today.AddHours(10);
        var store = StoreAt(new FakeTimeProvider(setAt), AgentIdentity.None);
        var project = await Arrangement.ArrangeProjectAsync();

        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Born at risk", changes: new WorkItemChanges { Health = new(FeatureHealth.AtRisk) });

        await using var connection = await Arrangement.Database.OpenAsync();
        (await ScreensRows.HealthAsync(connection, feature.Id)).ShouldBe(new ScreensRows.Health("atRisk", setAt, Actor, null));
        feature.Changes.Select(c => c.Field).ShouldContain("health");
    }

    [Test]
    public async Task Schema_WhenAStoryHasHealth_ShouldRefuseIt()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var error = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync("UPDATE work_item SET health = 'atRisk' WHERE id = @id", new { id = story.Id }));

        error.ConstraintName.ShouldBe("ck_work_item_health_feature_only");
    }

    [Test]
    public async Task WorkItemBuilder_WhenWithHealth_ShouldReadBackThroughScreensRows()
    {
        var project = await Arrangement.ArrangeProjectAsync();
        var setAt = TestDates.Today.AddHours(-30);
        await using var connection = await Arrangement.Database.OpenAsync();

        var id = await new WorkItemBuilder(project.ProjectId).WithType("feature").WithTitle("Arranged health")
            .WithHealth("atRisk", setAt, SeedIds.Users.Tom, "Claude Code (claude.ai)").InsertAsync(connection);

        (await ScreensRows.HealthAsync(connection, id)).ShouldBe(new ScreensRows.Health("atRisk", setAt, SeedIds.Users.Tom, "Claude Code (claude.ai)"));
        var item = await Arrangement.CreateReads().GetAsync(id);
        item.Health.ShouldBe(FeatureHealth.AtRisk);
        var set = item.HealthSet.ShouldNotBeNull();
        set.At.ShouldBe(setAt);
        set.By.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Tom);
        set.Agent.ShouldBe("Claude Code (claude.ai)");
    }
}
