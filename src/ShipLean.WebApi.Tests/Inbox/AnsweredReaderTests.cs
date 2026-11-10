using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inbox;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Asks;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Inbox;

// GET inbox/answered through the real host and Postgres. The clock is pinned at 12:00 UTC on 10 Nov in a UTC+7 zone, so the
// server's day began at 17:00 UTC on 9 Nov. Every test arranges its own signed-in user, items and events: the seed already
// holds answers for Tom.
[TestFixture]
public class AnsweredReaderTests : ApiFixture
{
    private const string Agent = "Claude Code (test)";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private static readonly DateTimeOffset Now = new(2026, 11, 10, 12, 0, 0, TimeSpan.Zero);

    private static readonly TimeZoneInfo Zone = TimeZoneInfo.CreateCustomTimeZone("UTC+7", TimeSpan.FromHours(7), "UTC+7", "UTC+7");

    private static readonly FakeTimeProvider Clock = CreateClock();

    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<TimeProvider>(Clock);

    private static FakeTimeProvider CreateClock()
    {
        var clock = new FakeTimeProvider(Now);
        clock.SetLocalTimeZone(Zone);
        return clock;
    }

    [Test]
    public async Task GetAnswered_WhenEightDecisionsConfirmedTogether_ShouldMakeOneRowApprovedD1ToD8()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection, "Pick the board layout");
        var at = Now.AddHours(-3);
        await DecisionEventsAsync(connection, item, me, "confirmed", at, 1, 2, 3, 4, 5, 6, 7, 8);

        var answered = await GetAsync(me);

        var row = answered.Rows.Single();
        row.ItemId.ShouldBe(item);
        row.Key.ShouldBe(await KeyAsync(connection, item));
        row.Type.ShouldBe("story");
        row.Title.ShouldBe("Pick the board layout");
        row.Kind.ShouldBe(InboxAskKind.Decision);
        row.Labels.ShouldBe("D1–D8");
        row.Summary.ShouldBe("Approved D1–D8");
        row.AnsweredAt.ShouldBe(at);
        row.AnsweredAtLabel.ShouldBe(DisplayDate.Relative(at, Clock));
        row.Next.ShouldBeNull();
    }

    [Test]
    public async Task GetAnswered_WhenNumbersHaveGap_ShouldCompressRange()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        await DecisionEventsAsync(connection, item, me, "confirmed", Now.AddHours(-1), 5, 1, 2, 3);

        var row = (await GetAsync(me)).Rows.Single();

        row.Labels.ShouldBe("D1–D3, D5");
        row.Summary.ShouldBe("Approved D1–D3, D5");
    }

    [Test]
    public async Task GetAnswered_WhenDecisionChanged_ShouldSayChanged()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        await DecisionEventsAsync(connection, item, me, "changed", Now.AddHours(-1), 4);

        var row = (await GetAsync(me)).Rows.Single();

        row.Kind.ShouldBe(InboxAskKind.Decision);
        row.Labels.ShouldBe("D4");
        row.Summary.ShouldBe("Changed D4");
    }

    [Test]
    public async Task GetAnswered_WhenQuestionAnswered_ShouldSayAnsweredQ1()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        await QuestionEventAsync(connection, item, me, "answered", Now.AddHours(-1), 1);

        var row = (await GetAsync(me)).Rows.Single();

        row.Kind.ShouldBe(InboxAskKind.Question);
        row.Labels.ShouldBe("Q1");
        row.Summary.ShouldBe("Answered Q1");
    }

    [Test]
    public async Task GetAnswered_WhenMentionRepliedOrSeen_ShouldListBoth()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        var repliedAt = Now.AddHours(-2);
        var seenAt = Now.AddHours(-4);
        await MentionAsync(connection, item, me, "replied", repliedAt, author: SeedIds.Users.Priya);
        await MentionAsync(connection, item, me, "seen", seenAt, author: SeedIds.Users.Priya);
        await MentionAsync(connection, item, me, "open", Now.AddHours(-1), author: SeedIds.Users.Priya);
        await MentionAsync(connection, item, me, "withdrawn", Now.AddHours(-1), author: SeedIds.Users.Priya);

        var rows = (await GetAsync(me)).Rows;

        rows.Select(r => r.Summary).ShouldBe(["Replied to Priya Shah", "Marked seen"]);
        rows.ShouldAllBe(r => r.Kind == InboxAskKind.Mention && r.Labels == "@");
        rows.Select(r => r.AnsweredAt).ShouldBe([repliedAt, seenAt]);
    }

    [Test]
    public async Task GetAnswered_WhenDecisionDiscussed_ShouldLeaveItOut()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        // Discuss, a status line still true, and the asks' own raise, edit, withdraw and ask events are none of them answers.
        var (first, _) = await new DecisionBuilder(item).Number(1).BuildAsync(connection);
        foreach (var kind in new[] { "raised", "edited", "withdrawn" })
        {
            await new AskEventBuilder(item).ForDecision(first).Kind(kind).By(me, Agent).At(Now.AddHours(-1)).BuildAsync(connection);
        }
        await QuestionEventAsync(connection, item, me, "asked", Now.AddHours(-1), 1);
        var comment = await new CommentBuilder(item).WithAuthor(me).WithBody("On D1: not sure yet").WithCreatedAt(Now.AddHours(-1)).InsertAsync(connection);
        var (decision, _) = await new DecisionBuilder(item).Number(2).BuildAsync(connection);
        await new AskTagBuilder().Comment(comment).Kind("about").Decision(decision).BuildAsync(connection);

        (await GetAsync(me)).Rows.ShouldBeEmpty();
    }

    [Test]
    public async Task GetAnswered_WhenAgentCommentsAfter_ShouldSetAgentPickedUp()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        var answeredAt = Now.AddHours(-5);
        await DecisionEventsAsync(connection, item, me, "confirmed", answeredAt, 1);
        // The earliest qualifying event wins, whatever else follows it, and an earlier comment doesn't count.
        await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).WithAgentName("Codex").WithCreatedAt(answeredAt.AddMinutes(-1)).InsertAsync(connection);
        var pickedUpAt = answeredAt.AddMinutes(20);
        await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).WithAgentName("Codex").WithCreatedAt(pickedUpAt).InsertAsync(connection);
        await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).WithCreatedAt(answeredAt.AddHours(1)).InsertAsync(connection);

        var next = (await GetAsync(me)).Rows.Single().Next!;

        next.Kind.ShouldBe(AnsweredNextKind.AgentPickedUp);
        next.AgentName.ShouldBe("Codex");
        next.By!.Id.ShouldBe(SeedIds.Users.Priya);
        next.At.ShouldBe(pickedUpAt);
        next.AtLabel.ShouldBe(DisplayDate.Relative(pickedUpAt, Clock));
    }

    [Test]
    public async Task GetAnswered_WhenMyOwnAgentRevisesAfter_ShouldSetAgentPickedUp()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        var answeredAt = Now.AddHours(-5);
        await DecisionEventsAsync(connection, item, me, "confirmed", answeredAt, 1);
        var revisedAt = answeredAt.AddMinutes(30);
        await new RevisionBuilder(item).WithRev(2).WithChangedBy(me).WithAgent("Claude Code (me)").WithChangedAt(revisedAt).InsertAsync(connection);

        var next = (await GetAsync(me)).Rows.Single().Next!;

        next.Kind.ShouldBe(AnsweredNextKind.AgentPickedUp);
        next.AgentName.ShouldBe("Claude Code (me)");
        next.By!.Id.ShouldBe(me);
        next.At.ShouldBe(revisedAt);
    }

    [Test]
    public async Task GetAnswered_WhenAnotherPersonCommentsAfter_ShouldSetPersonReplied()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        var answeredAt = Now.AddHours(-5);
        await DecisionEventsAsync(connection, item, me, "confirmed", answeredAt, 1);
        var repliedAt = answeredAt.AddMinutes(10);
        await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).WithCreatedAt(repliedAt).InsertAsync(connection);

        var next = (await GetAsync(me)).Rows.Single().Next!;

        next.Kind.ShouldBe(AnsweredNextKind.PersonReplied);
        next.By!.Id.ShouldBe(SeedIds.Users.Priya);
        next.AgentName.ShouldBeNull();
        next.At.ShouldBe(repliedAt);
    }

    [Test]
    public async Task GetAnswered_WhenOnlyMyPlainCommentAfter_ShouldLeaveNextNull()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        var answeredAt = Now.AddHours(-5);
        await DecisionEventsAsync(connection, item, me, "confirmed", answeredAt, 1);
        await new CommentBuilder(item).WithAuthor(me).WithCreatedAt(answeredAt.AddMinutes(10)).InsertAsync(connection);
        // An archived comment from another person doesn't count either.
        await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).WithCreatedAt(answeredAt.AddMinutes(11)).Archived().InsertAsync(connection);
        // Nor does a comment the answer itself posted, even through my agent.
        var (decision, _) = await new DecisionBuilder(item).Number(3).BuildAsync(connection);
        var changed = await new CommentBuilder(item).WithAuthor(me).WithAgentName(Agent).WithCreatedAt(answeredAt.AddMinutes(12)).InsertAsync(connection);
        await new AskTagBuilder().Comment(changed).Kind("changed").Decision(decision).BuildAsync(connection);

        (await GetAsync(me)).Rows.Single().Next.ShouldBeNull();
    }

    [Test]
    public async Task GetAnswered_WhenOnlyBulkRevisionAfter_ShouldLeaveNextNull()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        var answeredAt = Now.AddHours(-5);
        await DecisionEventsAsync(connection, item, me, "confirmed", answeredAt, 1);
        await new RevisionBuilder(item).WithRev(2).WithChangedBy(me).WithAgent(Agent).WithBulk().WithChangedAt(answeredAt.AddMinutes(10)).InsertAsync(connection);
        // A revision with no agent name is a person saving the item.
        await new RevisionBuilder(item).WithRev(3).WithChangedBy(SeedIds.Users.Priya).WithChangedAt(answeredAt.AddMinutes(11)).InsertAsync(connection);

        (await GetAsync(me)).Rows.Single().Next.ShouldBeNull();
    }

    [Test]
    public async Task GetAnswered_WhenOnlyMirroredGitHubCommentAfter_ShouldLeaveNextNull()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        var answeredAt = Now.AddHours(-5);
        await DecisionEventsAsync(connection, item, me, "confirmed", answeredAt, 1);
        var mirrored = await new CommentBuilder(item).WithAuthor(SeedIds.Users.Priya).WithAgentName("GitHub @reporter")
            .WithCreatedAt(answeredAt.AddMinutes(10)).InsertAsync(connection);
        await MirrorAsync(connection, item, mirrored);

        (await GetAsync(me)).Rows.Single().Next.ShouldBeNull();
    }

    [Test]
    public async Task GetAnswered_WhenAnsweredEightDaysAgo_ShouldLeaveItOut()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        await DecisionEventsAsync(connection, item, me, "confirmed", Now.AddDays(-8), 1);
        await MentionAsync(connection, item, me, "seen", Now.AddDays(-8), author: SeedIds.Users.Priya);
        var inside = Now.AddDays(-6).AddHours(-23);
        await DecisionEventsAsync(connection, item, me, "changed", inside, 2);

        var rows = (await GetAsync(me)).Rows;

        rows.Single().AnsweredAt.ShouldBe(inside);
    }

    [Test]
    public async Task GetAnswered_WhenAnsweredInAnotherProjectOrByAnotherUser_ShouldLeaveItOut()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var other = await MemberAsync(connection);
        var mine = await ItemAsync(connection, "Mine");
        var inPay = await ItemAsync(connection, "In Pay", SeedIds.Projects.Pay);
        var at = Now.AddHours(-1);
        await DecisionEventsAsync(connection, mine, me, "confirmed", at, 1);
        await DecisionEventsAsync(connection, mine, other, "confirmed", at, 2);
        await DecisionEventsAsync(connection, inPay, me, "confirmed", at, 1);
        await MentionAsync(connection, mine, other, "seen", at, author: SeedIds.Users.Priya);
        await MentionAsync(connection, inPay, me, "seen", at, author: SeedIds.Users.Priya);

        var rows = (await GetAsync(me)).Rows;

        rows.Single().ItemId.ShouldBe(mine);
        rows.Single().Labels.ShouldBe("D1");
    }

    [Test]
    public async Task GetAnswered_WhenFiftyFiveRows_ShouldReturnNewestFifty()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var item = await ItemAsync(connection);
        for (var minute = 1; minute <= 55; minute++)
        {
            await DecisionEventsAsync(connection, item, me, "confirmed", Now.AddMinutes(-minute), minute);
        }

        var answered = await GetAsync(me);

        answered.Rows.Count.ShouldBe(InboxRules.AnsweredLimit);
        answered.Rows.Select(r => r.AnsweredAt).ShouldBe(Enumerable.Range(1, 50).Select(minute => Now.AddMinutes(-minute)));
        answered.Rows.ShouldNotContain(r => r.Labels == "D51");
        // Counts cover every row today, not only the fifty returned.
        answered.AnsweredToday.ShouldBe(55);
    }

    [Test]
    public async Task GetAnswered_WhenAnsweredTodayAndPickedUp_ShouldCountBoth()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var pickedUp = await ItemAsync(connection, "Picked up");
        var waiting = await ItemAsync(connection, "Waiting");
        var replied = await ItemAsync(connection, "Replied");
        var yesterdayPickedUp = await ItemAsync(connection, "Yesterday picked up");
        var today = DisplayDate.StartOfDay(DisplayDate.Today(Now, Zone), Zone);
        today.ShouldBe(new DateTimeOffset(2026, 11, 9, 17, 0, 0, TimeSpan.Zero));

        // Just after the server's midnight counts as today (a UTC midnight would call it yesterday), and one minute before is not.
        var justToday = today.AddMinutes(1);
        var justYesterday = today.AddMinutes(-1);
        await DecisionEventsAsync(connection, pickedUp, me, "confirmed", justToday, 1);
        await DecisionEventsAsync(connection, waiting, me, "confirmed", Now.AddHours(-1), 1);
        await DecisionEventsAsync(connection, replied, me, "confirmed", Now.AddHours(-2), 1);
        await DecisionEventsAsync(connection, yesterdayPickedUp, me, "confirmed", justYesterday, 1);
        await new CommentBuilder(pickedUp).WithAuthor(SeedIds.Users.Priya).WithAgentName(Agent).WithCreatedAt(justToday.AddMinutes(5)).InsertAsync(connection);
        await new CommentBuilder(replied).WithAuthor(SeedIds.Users.Priya).WithCreatedAt(Now.AddHours(-1)).InsertAsync(connection);
        await new CommentBuilder(yesterdayPickedUp).WithAuthor(SeedIds.Users.Priya).WithAgentName(Agent).WithCreatedAt(justYesterday.AddMinutes(5)).InsertAsync(connection);

        var answered = await GetAsync(me);

        answered.Rows.Count.ShouldBe(4);
        answered.AnsweredToday.ShouldBe(3);
        answered.PickedUpToday.ShouldBe(1);
    }

    [Test]
    public async Task GetAnswered_WhenCallerNotMember_ShouldReturnNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"api/projects/{SeedIds.Projects.Ship}/inbox/answered");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    private async Task<AnsweredDto> GetAsync(Guid user)
    {
        using var response = await Api.ActingAs(TestUser.Of(user)).GetAsync($"api/projects/{SeedIds.Projects.Ship}/inbox/answered");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<AnsweredDto>(text, Wire)!;
    }

    private static async Task<Guid> MemberAsync(NpgsqlConnection connection)
    {
        var user = await new AppUserBuilder().InsertAsync(connection);
        await connection.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id) VALUES (@id, @project, @user)",
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, user });
        return user;
    }

    private static Task<Guid> ItemAsync(NpgsqlConnection connection, string title = "Arranged item", Guid? project = null)
        => new WorkItemBuilder(project ?? SeedIds.Projects.Ship).WithTitle(title).InsertAsync(connection);

    private static Task<string> KeyAsync(NpgsqlConnection connection, Guid item)
        => connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @item", new { item })!;

    // One event per number on the item's decisions, all at the same instant, as one batch confirm writes them.
    private static async Task DecisionEventsAsync(
        NpgsqlConnection connection, Guid item, Guid by, string kind, DateTimeOffset at, params int[] numbers)
    {
        foreach (var number in numbers)
        {
            var (decision, _) = await new DecisionBuilder(item).Number(number).BuildAsync(connection);
            await new AskEventBuilder(item).ForDecision(decision).Kind(kind).By(by, Agent).At(at).BuildAsync(connection);
        }
    }

    private static async Task QuestionEventAsync(NpgsqlConnection connection, Guid item, Guid by, string kind, DateTimeOffset at, int number)
    {
        var (question, _) = await new QuestionBuilder(item).Number(number).BuildAsync(connection);
        await new AskEventBuilder(item).ForQuestion(question).Kind(kind).By(by, Agent).At(at).BuildAsync(connection);
    }

    // A comment by author that mentions user, closed in the given state at closedAt.
    private static async Task MentionAsync(
        NpgsqlConnection connection, Guid item, Guid user, string state, DateTimeOffset closedAt, Guid author)
    {
        var comment = await new CommentBuilder(item).WithAuthor(author).WithCreatedAt(closedAt.AddHours(-1)).InsertAsync(connection);
        var builder = new MentionBuilder(comment, item, user).WithState(state).WithCreatedAt(closedAt.AddHours(-1));
        await (state is "open" ? builder : builder.WithClosedAt(closedAt)).InsertAsync(connection);
    }

    private static async Task MirrorAsync(NpgsqlConnection connection, Guid item, Guid comment)
    {
        var issue = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO github_issue (id, host, repository, number, project_id, work_item_id, author_login, title, body, phase, created_at, updated_at)
            VALUES (@issue, 'github.com', 'org/repo', 1, @project, @item, 'reporter', 'Issue', 'Body', 'accepted', now(), now())
            """,
            new { issue, project = SeedIds.Projects.Ship, item });
        await connection.ExecuteAsync(
            """
            INSERT INTO github_issue_comment (comment_id, github_issue_id, github_comment_id, html_url, author_login, body, created_at)
            VALUES (@comment, @issue, 1, 'https://github.com/org/repo/issues/1#issuecomment-1', 'reporter', 'Mirrored', now())
            """,
            new { comment, issue });
    }
}
