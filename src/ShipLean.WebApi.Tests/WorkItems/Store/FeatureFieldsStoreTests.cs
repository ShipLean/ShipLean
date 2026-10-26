using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Fakes.WorkItems;
using ShipLean.Application.Features.WorkItems;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// The Feature columns on Postgres: the read model's staleness rule on a clock the test sets, the agent stamp, and the
// schema's own checks behind the store's.
[TestFixture]
public class FeatureFieldsStoreTests : PostgresWorkItemStoreTestBase
{
    private static readonly DateTimeOffset Written = new(2026, 10, 1, 9, 0, 0, TimeSpan.Zero);
    private NpgsqlDataSource _dataSource = null!;

    [OneTimeSetUp]
    public void OpenDataSource() => _dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(Arrangement.Database.ConnectionString));

    [OneTimeTearDown]
    public async Task CloseDataSource() => await _dataSource.DisposeAsync();

    private WorkItemStore StoreAt(FakeTimeProvider clock, AgentIdentity? agent = null)
        => new(_dataSource, new FixedCurrentUser(Actor), new PassThroughWorkItemRules(), Arrangement.Events, new MarkdownRenderer(), clock, agent ?? AgentIdentity.None);

    [TestCase(14, 0, false)]
    [TestCase(14, 1, true)]
    [TestCase(15, 0, true)]
    [TestCase(2, 0, false)]
    public async Task Get_WhenVerifiedAfterTheStaleLimit_ShouldMarkStale(int days, int minutes, bool stale)
    {
        var clock = new FakeTimeProvider(Written);
        var store = StoreAt(clock);
        var project = await Arrangement.ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        await PatchAsync(store, feature.Id, 1, new WorkItemChanges { StatusLine = new("Going well") });
        clock.Advance(TimeSpan.FromDays(days) + TimeSpan.FromMinutes(minutes));

        var dto = await new WorkItemReads(_dataSource, new MarkdownRenderer(), clock).GetAsync(feature.Id);

        var verified = dto.StatusVerified.ShouldNotBeNull();
        verified.Stale.ShouldBe(stale);
        verified.At.ShouldBe(Written);
        verified.By.ShouldNotBeNull().Id.ShouldBe(Actor);
    }

    [Test]
    public async Task Get_WhenFeatureHasNoStatus_ShouldHaveNoVerifiedStamp()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var dto = await Arrangement.CreateReads().GetAsync(feature.Id);

        dto.Goal.ShouldBe("");
        dto.StatusLine.ShouldBe("");
        dto.StatusVerified.ShouldBeNull();
        dto.Slug.ShouldBeNull();
        dto.StartedOn.ShouldNotBeNull();
        var storyDto = await Arrangement.CreateReads().GetAsync(story.Id);
        storyDto.StartedOn.ShouldBeNull();
        storyDto.StartedLabel.ShouldBeNull();
    }

    [Test]
    public async Task Patch_WhenAnAgentWritesTheStatus_ShouldStampItsNameAndTool()
    {
        var clock = new FakeTimeProvider(Written);
        var store = StoreAt(clock, new AgentIdentity("Claude Code", "claude.ai", true));
        var project = await Arrangement.ArrangeProjectAsync();
        var feature = await CreateAsync(store, project, WorkItemTypeId.Feature, "Feature");

        WorkItemContractSupport.Written(await store.PatchAsync(
            feature.Id, new WorkItemPatch(1, new WorkItemChanges { StatusLine = new("Agent says so") }), new WriteOptions(null, RevisionOrigin.User, Via: "set_status")));

        var verified = (await new WorkItemReads(_dataSource, new MarkdownRenderer(), clock).GetAsync(feature.Id)).StatusVerified.ShouldNotBeNull();
        verified.Agent.ShouldBe("Claude Code (claude.ai)");
        verified.Via.ShouldBe("set_status");
        verified.AtLabel.ShouldBe("just now");
    }

    [Test]
    public async Task Schema_WhenAStoryHasFeatureColumns_ShouldRefuseThem()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var story = await CreateAsync(store, project, WorkItemTypeId.Story, "Story");

        var error = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync("UPDATE work_item SET goal = 'x' WHERE id = @id", new { id = story.Id }));

        error.ConstraintName.ShouldBe("ck_work_item_feature_fields_feature_only");
    }

    [Test]
    public async Task Schema_WhenTwoFeaturesShareASlug_ShouldRefuseTheSecond()
    {
        var store = PassThroughStore();
        var project = await Arrangement.ArrangeProjectAsync();
        var first = await CreateAsync(store, project, WorkItemTypeId.Feature, "First");
        var second = await CreateAsync(store, project, WorkItemTypeId.Feature, "Second");
        await ExecuteAsync("UPDATE work_item SET slug = 'shared' WHERE id = @id", new { id = first.Id });

        var error = await Should.ThrowAsync<PostgresException>(() => ExecuteAsync("UPDATE work_item SET slug = 'shared' WHERE id = @id", new { id = second.Id }));

        error.ConstraintName.ShouldBe("ux_work_item_slug");
    }
}
