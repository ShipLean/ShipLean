using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.FeaturesOverview;
using ShipLean.Application.Features.WorkItems.Reads.Changes;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Screens;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Contracts.Users;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Screens;

namespace ShipLean.WebApi.Tests.Screens;

// The Features digest through the real app on the seeded SHIP project. The database is shared by the class, so every test arranges
// its own work items, finds them in the answer by id, and acts as a fresh member who has never marked the digest seen (the seed has only Tom's row).
// Changes are placed relative to now; arranged items also carry their creation revision, written at the moment they are inserted.
[TestFixture]
public class FeatureDigestApiTests : ApiFixture
{
    private static readonly Guid Ship = SeedIds.Projects.Ship;

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static DateTimeOffset Ago(TimeSpan span) => TimeProvider.System.GetUtcNow() - span;

    // A fresh member of SHIP who has never seen the digest, so no other test's rows or marks reach them.
    private async Task<Guid> MemberAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var user = await new AppUserBuilder().InsertAsync(connection);
        await connection.ExecuteAsync("INSERT INTO project_member (id, project_id, user_id) VALUES (@id, @Ship, @user)", new { id = Guid.NewGuid(), Ship, user });
        return user;
    }

    // Every page of the digest, followed by cursor. The seed's own recent changes fill the first pages, so a test finds its items here.
    private async Task<IReadOnlyList<FeatureDigestDto>> PagesAsync(TestUser user)
    {
        var pages = new List<FeatureDigestDto> { await DigestAsync(user) };
        while (pages[^1].More)
        {
            pages.Add(await DigestAsync(user, pages[^1].Cursor));
        }

        return pages;
    }

    private static IEnumerable<DigestLineDto> LinesOf(IEnumerable<FeatureDigestDto> pages, Guid item) =>
        pages.SelectMany(p => p.Changed).Where(i => i.Id == item).SelectMany(i => i.Lines);

    private async Task<FeatureDigestDto> DigestAsync(TestUser user, string? cursor = null)
    {
        using var response = await Api.ActingAs(user).GetAsync($"api/projects/{Ship}/features/digest{(cursor is null ? string.Empty : $"?cursor={cursor}")}");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<FeatureDigestDto>(text, Wire)!;
    }

    private async Task<Guid> FeatureAsync(Action<WorkItemBuilder>? configure = null)
    {
        var builder = new WorkItemBuilder(Ship).WithType("feature");
        configure?.Invoke(builder);
        await using var connection = await OpenDatabaseAsync();
        return await builder.InsertAsync(connection);
    }

    private async Task RevisionAsync(Guid item, int rev, DateTimeOffset at, string changesJson, string? agent = null)
    {
        var builder = new RevisionBuilder(item).WithRev(rev).WithChangedAt(at).WithChangedBy(SeedIds.Users.Priya).WithChanges(changesJson);
        if (agent is not null)
        {
            builder.WithAgent(agent);
        }

        await using var connection = await OpenDatabaseAsync();
        await builder.InsertAsync(connection);
    }

    private static string StatusLine(string text) => $$"""[{"field":"statusLine","from":null,"to":"{{text}}","fromLabel":null,"toLabel":null}]""";

    private static string Health(string from, string to) => $$"""[{"field":"health","from":"{{from}}","to":"{{to}}","fromLabel":null,"toLabel":null}]""";

    [Test]
    public async Task Digest_WhenSeenTwoDaysAgo_ShouldListChangesSinceThen()
    {
        await using var connection = await OpenDatabaseAsync();
        var seenAt = (await ScreensRows.DigestSeenAsync(connection, SeedIds.Screens.DigestSeenUser, SeedIds.Screens.DigestSeenProject)).ShouldNotBeNull();
        var item = await FeatureAsync();
        await RevisionAsync(item, 2, seenAt.AddHours(-1), Health("onTrack", "atRisk"));
        await RevisionAsync(item, 3, seenAt.AddHours(1), StatusLine("Waiting on the fraud check API."));

        var pages = await PagesAsync(TestUser.Tom);

        pages[0].Since.ShouldBe(seenAt);
        var lines = LinesOf(pages, item).ToList();
        lines.ShouldContain(l => l.Kind == DigestLineKind.StatusLine && l.Text == "Waiting on the fraud check API.");
        lines.ShouldNotContain(l => l.Kind == DigestLineKind.Health);
    }

    [Test]
    public async Task Digest_WhenNeverSeen_ShouldUseTheLast24Hours()
    {
        var mei = TestUser.Of(await MemberAsync());
        var item = await FeatureAsync();
        await RevisionAsync(item, 2, Ago(TimeSpan.FromHours(30)), Health("onTrack", "atRisk"));
        await RevisionAsync(item, 3, Ago(TimeSpan.FromHours(2)), StatusLine("Two hours old."));

        var pages = await PagesAsync(mei);

        pages[0].Since.ShouldBe(Ago(TimeSpan.FromHours(24)), TimeSpan.FromMinutes(1));
        pages[0].SinceLabel.ShouldStartWith("since ");
        var lines = LinesOf(pages, item).ToList();
        lines.ShouldContain(l => l.Kind == DigestLineKind.StatusLine && l.Text == "Two hours old.");
        lines.ShouldNotContain(l => l.Kind == DigestLineKind.Health);
    }

    [Test]
    public async Task Digest_WhenAgentRaisedADecisionForMe_ShouldListItUnderNeedsYou()
    {
        var ana = await MemberAsync();
        var item = await FeatureAsync(b => b.WithTitle("Voice commands").WithAssignee(ana));
        await using (var connection = await OpenDatabaseAsync())
        {
            await new QuestionBuilder(item).Text("Parakeet or Whisper?").AskedBy(SeedIds.Users.Priya, null, Ago(TimeSpan.FromHours(2))).BuildAsync(connection);
            await new DecisionBuilder(item).Question("Approve the dark-mode colours?").RaisedBy(SeedIds.Users.Priya, "Claude Code", Ago(TimeSpan.FromHours(1))).BuildAsync(connection);
            await new DecisionBuilder(item).Question("Raised before I last looked").RaisedBy(SeedIds.Users.Priya, "Claude Code", Ago(TimeSpan.FromDays(3))).BuildAsync(connection);
        }

        var digest = await DigestAsync(TestUser.Of(ana));

        var row = digest.NeedsYou.Single(i => i.Id == item);
        (row.Type, row.Title).ShouldBe(("feature", "Voice commands"));
        row.Lines.Select(l => (l.Kind, l.Text)).ShouldBe([(DigestLineKind.Ask, "Needs input: Parakeet or Whisper?"), (DigestLineKind.Ask, "Decision: Approve the dark-mode colours?")]);
        row.Lines[0].By.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Priya);
        row.Lines[0].AgentName.ShouldBeNull();
        row.Lines[1].AgentName.ShouldBe("Claude Code");
        row.Lines[1].AtLabel.ShouldBe(DisplayDate.Relative(row.Lines[1].At, TimeProvider.System));
    }

    [Test]
    public async Task Digest_WhenAskIsSomeoneElses_ShouldLeaveItOut()
    {
        var ana = await MemberAsync();
        var assignedToSam = await FeatureAsync(b => b.WithAssignee(SeedIds.Users.Sam));
        var createdByMeAssignedToSam = await FeatureAsync(b => b.WithAssignee(SeedIds.Users.Sam).WithCreatedBy(ana));
        var unassignedCreatedBySam = await FeatureAsync(b => b.WithCreatedBy(SeedIds.Users.Sam));
        await using (var connection = await OpenDatabaseAsync())
        {
            foreach (var item in new[] { assignedToSam, createdByMeAssignedToSam, unassignedCreatedBySam })
            {
                await new DecisionBuilder(item).RaisedBy(SeedIds.Users.Priya, "Claude Code", Ago(TimeSpan.FromHours(1))).BuildAsync(connection);
            }
        }

        var digest = await DigestAsync(TestUser.Of(ana));

        digest.NeedsYou.Select(i => i.Id).ShouldNotContain(assignedToSam);
        digest.NeedsYou.Select(i => i.Id).ShouldNotContain(createdByMeAssignedToSam);
        digest.NeedsYou.Select(i => i.Id).ShouldNotContain(unassignedCreatedBySam);
    }

    [Test]
    public async Task Digest_WhenItemRemovedOrIHaveLeftTheProject_ShouldLeaveTheAskOut()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await new AppUserBuilder().InsertAsync(connection);
        await connection.ExecuteAsync("INSERT INTO project_member (id, project_id, user_id) VALUES (@id, @Ship, @me)", new { id = Guid.NewGuid(), Ship, me });
        var removed = await FeatureAsync(b => b.WithAssignee(me).WithStateNamed("Removed"));
        var live = await FeatureAsync(b => b.WithAssignee(me));
        foreach (var item in new[] { removed, live })
        {
            await new DecisionBuilder(item).RaisedBy(SeedIds.Users.Priya, "Claude Code", Ago(TimeSpan.FromHours(1))).BuildAsync(connection);
        }

        await using var dataSource = NpgsqlDataSource.Create(DatabaseServices.PinUtc(ConnectionString));
        var digest = new FeatureDigest(dataSource, new WorkItemChanges(dataSource), new SettableCurrentUser { Id = me }, TimeProvider.System);

        var whileMember = await digest.GetAsync(Ship, null, CancellationToken.None);
        await connection.ExecuteAsync("UPDATE project_member SET archived_at = now() WHERE project_id = @Ship AND user_id = @me", new { Ship, me });
        var afterLeaving = await digest.GetAsync(Ship, null, CancellationToken.None);

        whileMember.NeedsYou.Select(i => i.Id).ShouldBe([live]);
        afterLeaving.NeedsYou.ShouldBeEmpty();
    }

    [Test]
    public async Task Digest_WhenMoreThanAPage_ShouldPageByCursorWithNeedsYouOnFirstPageOnly()
    {
        var joel = TestUser.Of(await MemberAsync());
        var owned = await FeatureAsync(b => b.WithAssignee(joel.Id!.Value));
        var busy = await FeatureAsync();
        await using (var connection = await OpenDatabaseAsync())
        {
            await new DecisionBuilder(owned).Question("Which page?").RaisedBy(SeedIds.Users.Priya, null, Ago(TimeSpan.FromHours(1))).BuildAsync(connection);
            var first = Ago(TimeSpan.FromHours(3));
            for (var i = 0; i < 105; i++)
            {
                await new CommentBuilder(busy).WithBody($"Comment {i}").WithAuthor(SeedIds.Users.Priya).WithCreatedAt(first.AddSeconds(i)).InsertAsync(connection);
            }
        }

        var pages = await PagesAsync(joel);
        var page1 = pages[0];

        page1.More.ShouldBeTrue();
        page1.Cursor.ShouldNotBeNull();
        page1.NeedsYou.Select(i => i.Id).ShouldContain(owned);
        pages.Skip(1).ShouldNotBeEmpty();
        pages.Skip(1).ShouldAllBe(p => p.NeedsYou.Count == 0);
        pages.SelectMany(p => p.Changed).Where(i => i.Id == busy).SelectMany(i => i.Lines)
            .Count(l => l.Kind == DigestLineKind.Comment).ShouldBe(105);
    }

    [Test]
    public async Task Seen_WhenPosted_ShouldMoveSinceToNow()
    {
        var me = TestUser.Of(await MemberAsync());
        var item = await FeatureAsync();
        await RevisionAsync(item, 2, Ago(TimeSpan.FromHours(1)), StatusLine("Before I looked."));
        LinesOf(await PagesAsync(me), item).ShouldNotBeEmpty();

        using var seen = await Api.ActingAs(me).PostJsonAsync($"api/projects/{Ship}/features/digest/seen");
        var after = await PagesAsync(me);

        seen.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        after[0].Since.ShouldBe(Ago(TimeSpan.Zero), TimeSpan.FromMinutes(1));
        LinesOf(after, item).ShouldBeEmpty();
    }

    [Test]
    public async Task Seen_WhenPostedTwice_ShouldKeepOneRow()
    {
        var me = await MemberAsync();
        using var first = await Api.ActingAs(TestUser.Of(me)).PostJsonAsync($"api/projects/{Ship}/features/digest/seen");
        await using var connection = await OpenDatabaseAsync();
        var firstAt = (await ScreensRows.DigestSeenAsync(connection, me, Ship)).ShouldNotBeNull();
        using var second = await Api.ActingAs(TestUser.Of(me)).PostJsonAsync($"api/projects/{Ship}/features/digest/seen");

        (first.StatusCode, second.StatusCode).ShouldBe((HttpStatusCode.NoContent, HttpStatusCode.NoContent));
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM feature_digest_seen WHERE user_id = @user AND project_id = @Ship", new { user = me, Ship })).ShouldBe(1);
        (await ScreensRows.DigestSeenAsync(connection, me, Ship)).ShouldNotBeNull().ShouldBeGreaterThan(firstAt);
    }

    [Test]
    public async Task Digest_WhenNotMember_ShouldReturnNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"api/projects/{Ship}/features/digest");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Digest_WhenCursorInvalid_ShouldReturnValidation()
    {
        using var response = await Api.ActingAs(TestUser.Tom).GetAsync($"api/projects/{Ship}/features/digest?cursor=abc");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        (error.Code, error.Field).ShouldBe((ApiErrorCodes.Validation, "cursor"));
    }
}
