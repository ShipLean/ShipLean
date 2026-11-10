using System.Net;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inbox;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Inbox;

namespace ShipLean.WebApi.Tests.Inbox;

// POST api/mentions/{id}/seen and /reply through the real app, as the phone calls them: only the mention's own user, while a Member
// of its item's project, finds it; anyone else gets not_found and nothing changes. The database is shared by the class, so each
// test arranges its own user, item and comment, and the seeded mentions are only read.
[TestFixture]
public class MentionWritesTests : ApiFixture
{
    [Test]
    public async Task Seen_WhenOpen_ShouldMarkSeenWithClosedAt()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, SeedIds.Users.Priya);
        var mention = await new MentionBuilder(comment, item, me).InsertAsync(connection);

        using var response = await Api.ActingAs(TestUser.Of(me)).PostJsonAsync($"api/mentions/{mention}/seen");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var row = await RowAsync(connection, comment, mention);
        row.State.ShouldBe("seen");
        row.ClosedAt.ShouldNotBeNull();
        row.ReplyCommentId.ShouldBeNull();
        (await CommentCountAsync(connection, item)).ShouldBe(1);
    }

    [TestCase("seen")]
    [TestCase("replied")]
    [TestCase("withdrawn")]
    public async Task Seen_WhenAlreadySeen_ShouldChangeNothing(string state)
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, SeedIds.Users.Priya);
        var closedAt = TestDates.Today.AddDays(-1);
        var mention = await new MentionBuilder(comment, item, me).WithState(state).WithClosedAt(closedAt).InsertAsync(connection);

        using var response = await Api.ActingAs(TestUser.Of(me)).PostJsonAsync($"api/mentions/{mention}/seen");

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var row = await RowAsync(connection, comment, mention);
        row.State.ShouldBe(state);
        row.ClosedAt.ShouldBe(closedAt);
    }

    [Test]
    public async Task Seen_WhenMentionIsAnotherUsers_ShouldReturnNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var someoneElse = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, SeedIds.Users.Priya);
        var mention = await new MentionBuilder(comment, item, someoneElse).InsertAsync(connection);

        using var response = await Api.ActingAs(TestUser.Of(me)).PostJsonAsync($"api/mentions/{mention}/seen");

        await ShouldBeNotFoundAsync(response);
        (await RowAsync(connection, comment, mention)).State.ShouldBe("open");
    }

    [Test]
    public async Task Reply_WhenMentionIsAnotherUsers_ShouldReturnNotFoundAndPostNothing()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var someoneElse = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, SeedIds.Users.Priya);
        var mention = await new MentionBuilder(comment, item, someoneElse).InsertAsync(connection);

        using var response = await Api.ActingAs(TestUser.Of(me))
            .PostJsonAsync($"api/mentions/{mention}/reply", new MentionReplyRequest("Not mine to answer."));

        await ShouldBeNotFoundAsync(response);
        (await RowAsync(connection, comment, mention)).State.ShouldBe("open");
        (await CommentCountAsync(connection, item)).ShouldBe(1);
    }

    [Test]
    public async Task Seen_WhenIdUnknown_ShouldReturnNotFound()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);

        using var response = await Api.ActingAs(TestUser.Of(me)).PostJsonAsync($"api/mentions/{Guid.NewGuid()}/seen");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Reply_WhenUserRemovedFromProject_ShouldReturnNotFoundAndPostNothing()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, SeedIds.Users.Priya);
        var mention = await new MentionBuilder(comment, item, me).InsertAsync(connection);
        await LeaveAsync(connection, me);

        using var response = await Api.ActingAs(TestUser.Of(me))
            .PostJsonAsync($"api/mentions/{mention}/reply", new MentionReplyRequest("Too late."));

        await ShouldBeNotFoundAsync(response);
        (await RowAsync(connection, comment, mention)).State.ShouldBe("open");
        (await CommentCountAsync(connection, item)).ShouldBe(1);
    }

    [Test]
    public async Task Reply_WhenOpen_ShouldPostPrefixedCommentAndSetReplyCommentId()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, SeedIds.Users.Priya);
        var mention = await new MentionBuilder(comment, item, me).InsertAsync(connection);
        var priya = await connection.ExecuteScalarAsync<string>("SELECT name FROM app_user WHERE id = @id", new { id = SeedIds.Users.Priya });

        using var response = await Api.ActingAs(TestUser.Of(me))
            .PostJsonAsync($"api/mentions/{mention}/reply", new MentionReplyRequest("  Looking at it **now**.  "));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var row = await RowAsync(connection, comment, mention);
        row.State.ShouldBe("replied");
        row.ClosedAt.ShouldNotBeNull();
        var reply = await StoredCommentAsync(connection, row.ReplyCommentId!.Value);
        reply.Body.ShouldBe($"@{priya}, Looking at it **now**.");
        reply.AuthorId.ShouldBe(me);
        reply.WorkItemId.ShouldBe(item);
        reply.AgentName.ShouldBeNull();
        (await CommentCountAsync(connection, item)).ShouldBe(2);
    }

    [Test]
    public async Task Reply_WhenAuthorIsMe_ShouldPostWithoutPrefix()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, me);
        var mention = await new MentionBuilder(comment, item, me).InsertAsync(connection);

        using var response = await Api.ActingAs(TestUser.Of(me))
            .PostJsonAsync($"api/mentions/{mention}/reply", new MentionReplyRequest("Note to self: done."));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var row = await RowAsync(connection, comment, mention);
        (await StoredCommentAsync(connection, row.ReplyCommentId!.Value)).Body.ShouldBe("Note to self: done.");
    }

    [Test]
    public async Task Reply_WhenAuthorLeftProject_ShouldPostWithoutPrefix()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var author = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, author);
        var mention = await new MentionBuilder(comment, item, me).InsertAsync(connection);
        await LeaveAsync(connection, author);

        using var response = await Api.ActingAs(TestUser.Of(me))
            .PostJsonAsync($"api/mentions/{mention}/reply", new MentionReplyRequest("Picked this up."));

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var row = await RowAsync(connection, comment, mention);
        (await StoredCommentAsync(connection, row.ReplyCommentId!.Value)).Body.ShouldBe("Picked this up.");
    }

    [Test]
    public async Task Reply_WhenWithdrawn_ShouldReturnConflict()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, SeedIds.Users.Priya);
        var mention = await new MentionBuilder(comment, item, me).WithState("withdrawn").InsertAsync(connection);

        using var response = await Api.ActingAs(TestUser.Of(me))
            .PostJsonAsync($"api/mentions/{mention}/reply", new MentionReplyRequest("Answering anyway."));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.Conflict)).Code.ShouldBe(ApiErrorCodes.Conflict);
        (await RowAsync(connection, comment, mention)).State.ShouldBe("withdrawn");
        (await CommentCountAsync(connection, item)).ShouldBe(1);
    }

    [TestCase("")]
    [TestCase(" \n\t ")]
    public async Task Reply_WhenBodyEmpty_ShouldReturnValidationOnBody(string body)
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, SeedIds.Users.Priya);
        var mention = await new MentionBuilder(comment, item, me).InsertAsync(connection);

        using var response = await Api.ActingAs(TestUser.Of(me)).PostJsonAsync($"api/mentions/{mention}/reply", new MentionReplyRequest(body));

        await ShouldBeValidationOnBodyAsync(response);
        (await RowAsync(connection, comment, mention)).State.ShouldBe("open");
        (await CommentCountAsync(connection, item)).ShouldBe(1);
    }

    [Test]
    public async Task Reply_WhenSentTwiceAtOnce_ShouldPostTwoCommentsAndStayReplied()
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, SeedIds.Users.Priya);
        var mention = await new MentionBuilder(comment, item, me).InsertAsync(connection);
        var api = Api.ActingAs(TestUser.Of(me));

        var responses = await Task.WhenAll(
            api.PostJsonAsync($"api/mentions/{mention}/reply", new MentionReplyRequest("First tap.")),
            api.PostJsonAsync($"api/mentions/{mention}/reply", new MentionReplyRequest("Second tap.")));

        foreach (var response in responses)
        {
            response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
            response.Dispose();
        }
        var replies = (await connection.QueryAsync<Guid>(
            "SELECT id FROM comment WHERE work_item_id = @item AND author_id = @me", new { item, me })).ToList();
        replies.Count.ShouldBe(2);
        var row = await RowAsync(connection, comment, mention);
        row.State.ShouldBe("replied");
        replies.ShouldContain(row.ReplyCommentId!.Value);
    }

    // Wave amendment 6: the mention routes are kept out of the access sweep (RouteAllowList.CallerOwnedRoutes), so these stand in for it.
    [TestCase("seen")]
    [TestCase("reply")]
    public async Task MentionRoutes_WhenAnotherUsersMention_ShouldReturnNotFound(string route)
    {
        await using var connection = await OpenDatabaseAsync();
        var before = await RowAsync(connection, SeedIds.Inbox.OpenMentionComment, SeedIds.Inbox.OpenMention);

        // Priya wrote the comment and is a Member of SHIP, but the mention is Tom's.
        using var response = await Api.ActingAs(TestUser.Priya)
            .PostJsonAsync($"api/mentions/{SeedIds.Inbox.OpenMention}/{route}", new MentionReplyRequest("Answering for Tom."));

        await ShouldBeNotFoundAsync(response);
        (await RowAsync(connection, SeedIds.Inbox.OpenMentionComment, SeedIds.Inbox.OpenMention)).ShouldBe(before);
    }

    [TestCase("seen")]
    [TestCase("reply")]
    public async Task MentionRoutes_WhenCallerLeftProject_ShouldReturnNotFound(string route)
    {
        await using var connection = await OpenDatabaseAsync();
        var me = await MemberAsync(connection);
        var (item, comment) = await CommentAsync(connection, SeedIds.Users.Priya);
        var mention = await new MentionBuilder(comment, item, me).InsertAsync(connection);
        await LeaveAsync(connection, me);

        using var response = await Api.ActingAs(TestUser.Of(me))
            .PostJsonAsync($"api/mentions/{mention}/{route}", new MentionReplyRequest("Still here?"));

        await ShouldBeNotFoundAsync(response);
        (await RowAsync(connection, comment, mention)).State.ShouldBe("open");
        (await CommentCountAsync(connection, item)).ShouldBe(1);
    }

    [Test]
    public async Task MentionReply_WhenBodyBlankOnAnotherUsersMention_ShouldReturnValidationOnBody()
    {
        // The 400 depends only on the body, so it tells Priya nothing about Tom's mention.
        using var response = await Api.ActingAs(TestUser.Priya)
            .PostJsonAsync($"api/mentions/{SeedIds.Inbox.OpenMention}/reply", new MentionReplyRequest("   "));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("body");
    }

    private static async Task ShouldBeNotFoundAsync(HttpResponseMessage response) =>
        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);

    private static async Task ShouldBeValidationOnBodyAsync(HttpResponseMessage response)
    {
        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("body");
    }

    private static async Task<InboxRows.Mention> RowAsync(NpgsqlConnection connection, Guid comment, Guid mention) =>
        (await InboxRows.MentionsAsync(connection, comment)).Single(m => m.Id == mention);

    private static Task<int> CommentCountAsync(NpgsqlConnection connection, Guid item) =>
        connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM comment WHERE work_item_id = @item", new { item });

    private static Task<StoredComment> StoredCommentAsync(NpgsqlConnection connection, Guid id) =>
        connection.QuerySingleAsync<StoredComment>(
            "SELECT work_item_id AS workItemId, author_id AS authorId, body, agent_name AS agentName FROM comment WHERE id = @id", new { id });

    // A new SHIP story with one comment by author.
    private static async Task<(Guid Item, Guid Comment)> CommentAsync(NpgsqlConnection connection, Guid author)
    {
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
        var comment = await new CommentBuilder(item).WithAuthor(author).WithBody("Can you take a look?").InsertAsync(connection);
        return (item, comment);
    }

    private static async Task<Guid> MemberAsync(NpgsqlConnection connection)
    {
        var user = await new AppUserBuilder().InsertAsync(connection);
        await connection.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id) VALUES (@id, @project, @user)",
            new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, user });
        return user;
    }

    private static Task LeaveAsync(NpgsqlConnection connection, Guid user) =>
        connection.ExecuteAsync(
            "UPDATE project_member SET archived_at = now() WHERE user_id = @user AND project_id = @project",
            new { user, project = SeedIds.Projects.Ship });

    private sealed record StoredComment(Guid WorkItemId, Guid? AuthorId, string Body, string? AgentName);
}
