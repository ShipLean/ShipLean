using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.FeaturesOverview;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Screens;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Screens;
using ShipLean.WebApi.Tests.Backlog.Stories;

namespace ShipLean.WebApi.Tests.Screens;

// The Features overview through the real app on the seeded SHIP project. The database is shared by the class, so every test arranges its
// own Features and finds them in the answer by id. Each acts as a fresh member, so no one else's digest-seen row reaches it.
[TestFixture]
public class FeaturesOverviewApiTests : ApiFixture
{
    private static readonly Guid Ship = SeedIds.Projects.Ship;

    private static readonly Dictionary<Guid, NeedsYouDto> NeedsYou = [];

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<INeedsYouCounts>(new FakeNeedsYouCounts(NeedsYou));

    private static DateTimeOffset Ago(TimeSpan span) => TimeProvider.System.GetUtcNow() - span;

    private async Task<TestUser> MemberAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var user = await new AppUserBuilder().InsertAsync(connection);
        await connection.ExecuteAsync("INSERT INTO project_member (id, project_id, user_id) VALUES (@id, @Ship, @user)", new { id = Guid.NewGuid(), Ship, user });
        return TestUser.Of(user);
    }

    private async Task<FeaturesOverviewDto> OverviewAsync(TestUser? user = null)
    {
        using var response = await Api.ActingAs(user ?? await MemberAsync()).GetAsync($"api/projects/{Ship}/features/overview");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<FeaturesOverviewDto>(text, Wire)!;
    }

    private static FeatureRowDto Find(FeaturesOverviewDto overview, Guid id) =>
        overview.Groups.SelectMany(g => g.Rows).Single(r => r.Id == id);

    private async Task<Guid> InsertAsync(Action<WorkItemBuilder> configure)
    {
        var builder = new WorkItemBuilder(Ship);
        configure(builder);
        await using var connection = await OpenDatabaseAsync();
        return await builder.InsertAsync(connection);
    }

    private Task<Guid> FeatureAsync(Action<WorkItemBuilder>? configure = null) =>
        InsertAsync(b =>
        {
            b.WithType("feature");
            configure?.Invoke(b);
        });

    private async Task RevisionAsync(Guid item, int rev, DateTimeOffset at)
    {
        await using var connection = await OpenDatabaseAsync();
        await new RevisionBuilder(item).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Priya)
            .WithChanges("""[{"field":"title","from":"a","to":"b","fromLabel":null,"toLabel":null}]""").InsertAsync(connection);
    }

    [Test]
    public async Task Overview_WhenFeaturesSeeded_ShouldGroupWithHealthAndStatus()
    {
        await FeatureAsync(b => b.WithStateNamed("Closed"));
        await FeatureAsync(b => b.WithStateNamed("Closed"));

        var overview = await OverviewAsync();

        overview.Groups.Select(g => g.Label).ShouldBe(["Active", "Resolved", "New", "Closed"].Where(l => overview.Groups.Any(g => g.Label == l)));
        Find(overview, SeedIds.Screens.AtRiskFeature).Health.ShouldBe(FeatureHealth.AtRisk);
        Find(overview, SeedIds.Screens.BlockedFeature).Health.ShouldBe(FeatureHealth.Blocked);
        Find(overview, SeedIds.Screens.AtRiskFeature).StatusLine.ShouldNotBeNullOrEmpty();
        var closed = overview.Groups.Single(g => g.Label == "Closed");
        closed.Folded.ShouldBeTrue();
        closed.Count.ShouldBeGreaterThanOrEqualTo(2);
        closed.Rows.ShouldBeEmpty();
        overview.Groups.Where(g => !g.Folded).ShouldAllBe(g => g.Rows.Count == g.Count);
    }

    [Test]
    public async Task Overview_WhenStatusVerifiedFourDaysAgo_ShouldMarkStale()
    {
        var stale = await FeatureAsync(b => b.WithStatusLine("Four days old.", Ago(TimeSpan.FromDays(4)), SeedIds.Users.Priya, "Claude Code"));
        var unverified = await FeatureAsync(b => b.WithStatusLine("Never verified.", null, null, null));
        var empty = await FeatureAsync();

        var overview = await OverviewAsync();

        var row = Find(overview, stale);
        row.StatusStale.ShouldBeTrue();
        row.StatusLine.ShouldBe("Four days old.");
        row.StatusVerified.ShouldNotBeNull().Stale.ShouldBeFalse();
        row.StatusVerified!.By.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Priya);
        row.StatusVerified.Agent.ShouldBe("Claude Code");
        Find(overview, unverified).StatusStale.ShouldBeTrue();
        Find(overview, empty).StatusStale.ShouldBeFalse();
    }

    [Test]
    public async Task Overview_WhenVerifiedToday_ShouldNotMarkStale()
    {
        var item = await FeatureAsync(b => b.WithStatusLine("Fresh.", Ago(TimeSpan.FromHours(2)), SeedIds.Users.Priya, null));

        var row = Find(await OverviewAsync(), item);

        row.StatusStale.ShouldBeFalse();
        row.StatusVerified.ShouldNotBeNull();
    }

    [Test]
    public async Task Overview_WhenFeatureHasStories_ShouldCarryRollupsWithoutTasks()
    {
        var feature = await FeatureAsync();
        var story = await InsertAsync(b => b.WithParent(feature));
        await InsertAsync(b => b.WithParent(feature));
        await InsertAsync(b => b.WithType("task").WithParent(story));
        await InsertAsync(b => b.WithType("task").WithParent(feature));

        var row = Find(await OverviewAsync(), feature);

        row.Rollups.ShouldNotBeEmpty();
        row.Rollups.ShouldAllBe(r => r.Type != WorkItemTypeId.Task);
        row.Rollups.Single(r => r.Type == WorkItemTypeId.Story).Open.ShouldBe(2);
    }

    [Test]
    public async Task Overview_WhenRanked_ShouldUseProjectWideRank()
    {
        await FeatureAsync(b => b.WithStateNamed("Closed"));
        var backlog = await Api.GetBacklogAsync($"api/projects/{Ship}/backlogs/features?includeCompleted=true");
        var expected = backlog.Rows.ToDictionary(r => r.Id, r => r.Rank);

        var overview = await OverviewAsync();

        var rows = overview.Groups.SelectMany(g => g.Rows).ToList();
        rows.ShouldNotBeEmpty();
        rows.ShouldAllBe(r => r.Rank == expected[r.Id]);
        // A group's first row is not rank 1 when other Features come before it in the backlog.
        overview.Groups.Select(g => g.Rows).Where(r => r.Count > 0).Any(r => r[0].Rank > 1).ShouldBeTrue();
        overview.Groups.ShouldAllBe(g => g.Rows.Select(r => r.Rank).SequenceEqual(g.Rows.Select(r => r.Rank).Order()));
    }

    [Test]
    public async Task Overview_WhenFeatureUnderEpic_ShouldCarryTheEpic()
    {
        var epic = await InsertAsync(b => b.WithType("epic").WithTitle("Payments"));
        var feature = await FeatureAsync(b => b.WithParent(epic));

        var row = Find(await OverviewAsync(), feature);

        var parent = row.Epic.ShouldNotBeNull();
        parent.Id.ShouldBe(epic);
        parent.Title.ShouldBe("Payments");
        parent.Type.Id.ShouldBe(WorkItemTypeId.Epic);
    }

    [Test]
    public async Task Overview_WhenNoEpic_ShouldHaveNullEpic()
    {
        var outer = await FeatureAsync();
        var nested = await FeatureAsync(b => b.WithParent(outer));

        var overview = await OverviewAsync();

        Find(overview, outer).Epic.ShouldBeNull();
        Find(overview, nested).Epic.ShouldBeNull();
    }

    [Test]
    public async Task Overview_WhenNeedsYouCountsGiven_ShouldCarryThemAndNullOtherwise()
    {
        var waiting = await FeatureAsync();
        var quiet = await FeatureAsync();
        var user = await MemberAsync();
        NeedsYou[waiting] = new NeedsYouDto(1, 2, true);
        try
        {
            var overview = await OverviewAsync(user);

            Find(overview, waiting).NeedsYou.ShouldBe(new NeedsYouDto(1, 2, true));
            Find(overview, quiet).NeedsYou.ShouldBeNull();
        }
        finally
        {
            NeedsYou.Remove(waiting);
        }
    }

    [Test]
    public async Task Overview_WhenSeenTwoDaysAgo_ShouldCountChangesSinceThen()
    {
        var user = await MemberAsync();
        var seenAt = Ago(TimeSpan.FromDays(2));
        await using (var connection = await OpenDatabaseAsync())
        {
            await new DigestSeenBuilder(user.Id, Ship).WithSeenAt(seenAt).InsertAsync(connection);
        }

        var before = await OverviewAsync(user);
        var item = await FeatureAsync();
        await RevisionAsync(item, 2, seenAt.AddHours(1));
        await RevisionAsync(item, 3, seenAt.AddHours(-1));
        var after = await OverviewAsync(user);

        // The creation revision (now) and the one an hour after the mark count; the one before it does not.
        (after.ChangedSince - before.ChangedSince).ShouldBe(2);
        after.SinceLabel.ShouldBe(before.SinceLabel);
        after.SinceLabel.ShouldStartWith("since ");
    }

    [Test]
    public async Task Overview_WhenNeverSeen_ShouldUseTheLast24Hours()
    {
        var user = await MemberAsync();

        var before = await OverviewAsync(user);
        var item = await FeatureAsync();
        await RevisionAsync(item, 2, Ago(TimeSpan.FromHours(2)));
        await RevisionAsync(item, 3, Ago(TimeSpan.FromHours(30)));
        var after = await OverviewAsync(user);

        (after.ChangedSince - before.ChangedSince).ShouldBe(2);
        after.SinceLabel.ShouldStartWith("since ");
    }

    [Test]
    public async Task Overview_WhenNotMember_ShouldReturnNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"api/projects/{Ship}/features/overview");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
