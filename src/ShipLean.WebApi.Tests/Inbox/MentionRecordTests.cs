using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Inbox;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Inbox;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Inbox;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Inbox;

// P8 through the real app: every comment saved on the web, through the MCP comment tool or as an agent records a
// comment_mention row per live SHIP member named as @Full Name; imports and GitHub mirrors record none; an edit adds new
// names and withdraws open mentions whose name is gone. SHIP's members are Joel, Priya, Tom, Mei, Sam and Ana; Kai is in no project.
[TestFixture]
public class MentionRecordTests : McpFixture
{
    private static readonly Guid Item = SeedIds.WorkItems.Ship90;
    private static readonly string CommentsUrl = $"api/work-items/{Item}/comments";

    [Test]
    public async Task AddComment_WhenWebCommentMentionsMember_ShouldRecordOpenMention()
    {
        var comment = await AddAsync(TestUser.Joel, "@Mei Lin can you take a look?");

        var mention = (await MentionsAsync(comment.Id)).ShouldHaveSingleItem();
        mention.UserId.ShouldBe(SeedIds.Users.Mei);
        mention.WorkItemId.ShouldBe(Item);
        mention.State.ShouldBe("open");
        mention.ClosedAt.ShouldBeNull();
        mention.ReplyCommentId.ShouldBeNull();
    }

    [Test]
    public async Task CommentTool_WhenMcpCommentMentionsMember_ShouldRecordMention()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "comment", """{"item":"SHIP-90","text":"Handing over to @Sam Rivera."}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var commentId = Guid.Parse(reply.Structured().GetProperty("comment_id").GetString()!);
        (await MentionsAsync(commentId)).Select(m => m.UserId).ShouldBe([SeedIds.Users.Sam]);
    }

    [Test]
    public async Task AddAsAgent_WhenAgentReportMentionsMember_ShouldRecordMention()
    {
        await using var scope = Host.Services.CreateAsyncScope();

        var comment = await scope.ServiceProvider.GetRequiredService<ICommentStore>().AddAsAgentAsync(
            Item, new CommentCreate("Done. @Priya Shah, the PR is up."), "Relay", SeedIds.Users.Joel);

        (await MentionsAsync(comment.Id)).Select(m => m.UserId).ShouldBe([SeedIds.Users.Priya]);
    }

    [Test]
    public async Task AddAsAgent_WhenAgentMentionsItsOwnOperator_ShouldRecordMention()
    {
        await using var scope = Host.Services.CreateAsyncScope();

        var comment = await scope.ServiceProvider.GetRequiredService<ICommentStore>().AddAsAgentAsync(
            Item, new CommentCreate("@Joel Dickson I need a decision on D2."), "Relay", SeedIds.Users.Joel);

        (await MentionsAsync(comment.Id)).Select(m => m.UserId).ShouldBe([SeedIds.Users.Joel]);
    }

    [Test]
    public async Task AddComment_WhenPersonMentionsSelf_ShouldRecordNothing()
    {
        var comment = await AddAsync(TestUser.Mei, "Note to self, @Mei Lin: check the totals.");

        (await MentionsAsync(comment.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task AddComment_WhenNameIsNonMemberOrRemovedMember_ShouldRecordNothing()
    {
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync(
                "UPDATE project_member SET archived_at = now() WHERE project_id = @project AND user_id = @ana",
                new { project = SeedIds.Projects.Ship, ana = SeedIds.Users.Ana });
        }

        var comment = await AddAsync(TestUser.Joel, "@Kai Moreno and @Ana Costa, and @Tom Okafor too");

        (await MentionsAsync(comment.Id)).Select(m => m.UserId).ShouldBe([SeedIds.Users.Tom]);
    }

    [Test]
    public async Task Import_WhenBodyHasMention_ShouldRecordNothing()
    {
        await using var scope = Host.Services.CreateAsyncScope();

        var id = await scope.ServiceProvider.GetRequiredService<ICommentStore>().ImportAsync(
            Item, new ImportedComment("jira", $"mention-{Guid.NewGuid():N}", SeedIds.Users.Joel, "@Mei Lin imported this", TestDates.Today.AddDays(-3)));

        (await MentionsAsync(id)).ShouldBeEmpty();
    }

    [Test]
    public async Task MirrorComment_WhenGitHubBodyHasMention_ShouldRecordNothing()
    {
        var issue = await Host.Services.GetRequiredService<IGitHubIssueStore>().InsertAsync(
            new IssueRef(IssueRef.GitHubHost, "shiplean-tests/mentions", Random.Shared.Next(1, int.MaxValue)), SeedIds.Projects.Ship, SeedIds.WorkItems.Ship89,
            "reporter", "The title", "The body", IssuePhase.Proposing);
        var githubId = Random.Shared.NextInt64(1, long.MaxValue);
        var mirrored = new MirroredComment(
            issue.Id, githubId, $"https://github.com/{issue.Issue.Repository}/issues/{issue.Issue.Number}#issuecomment-{githubId}", "reporter",
            "@Mei Lin please fix this", TestDates.Today, SeedIds.Users.Joel);
        await using var scope = Host.Services.CreateAsyncScope();
        await using var connection = await OpenDatabaseAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var added = await scope.ServiceProvider.GetRequiredService<IMirroredComments>().AddAsync(mirrored, transaction);
        await transaction.CommitAsync();

        (await MentionsAsync(added.CommentId)).ShouldBeEmpty();
    }

    [Test]
    public async Task EditComment_WhenNameAdded_ShouldRecordNewMention()
    {
        var comment = await AddAsync(TestUser.Joel, "@Mei Lin first");

        await EditAsync(TestUser.Joel, comment.Id, "@Mei Lin first, then @Sam Rivera");

        var mentions = await MentionsAsync(comment.Id);
        mentions.Select(m => (m.UserId, m.State)).ShouldBe([(SeedIds.Users.Mei, "open"), (SeedIds.Users.Sam, "open")]);
    }

    [Test]
    public async Task EditComment_WhenNameRemoved_ShouldWithdrawOpenMention()
    {
        var comment = await AddAsync(TestUser.Joel, "@Mei Lin and @Sam Rivera");

        await EditAsync(TestUser.Joel, comment.Id, "Only @Sam Rivera now");

        var mentions = await MentionsAsync(comment.Id);
        var mei = mentions.Single(m => m.UserId == SeedIds.Users.Mei);
        mei.State.ShouldBe("withdrawn");
        mei.ClosedAt.ShouldNotBeNull();
        var sam = mentions.Single(m => m.UserId == SeedIds.Users.Sam);
        sam.State.ShouldBe("open");
        sam.ClosedAt.ShouldBeNull();
    }

    [Test]
    public async Task EditComment_WhenNameRemovedFromSeenMention_ShouldLeaveItSeen()
    {
        var seenAt = TestDates.Today.AddHours(-2);
        Guid commentId;
        await using (var connection = await OpenDatabaseAsync())
        {
            commentId = await new CommentBuilder(Item).WithAuthor(SeedIds.Users.Joel).WithBody("@Mei Lin have a look").InsertAsync(connection);
            await new MentionBuilder(commentId, Item, SeedIds.Users.Mei).WithState("seen").WithClosedAt(seenAt).InsertAsync(connection);
        }

        await EditAsync(TestUser.Joel, commentId, "Never mind, sorted");

        var mention = (await MentionsAsync(commentId)).ShouldHaveSingleItem();
        mention.State.ShouldBe("seen");
        mention.ClosedAt.ShouldBe(seenAt);
    }

    private async Task<CommentDto> AddAsync(TestUser user, string body)
    {
        using var response = await Api.ActingAs(user).PostJsonAsync(CommentsUrl, new CommentCreate(body));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CommentDto>())!;
    }

    private async Task EditAsync(TestUser user, Guid commentId, string body)
    {
        using var response = await Api.ActingAs(user).PatchJsonAsync($"api/comments/{commentId}", new CommentEdit(body));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    private async Task<IReadOnlyList<InboxRows.Mention>> MentionsAsync(Guid commentId)
    {
        await using var connection = await OpenDatabaseAsync();
        return await InboxRows.MentionsAsync(connection, commentId);
    }

    // The save fails after the mentions are written: the recorder double records through the real recorder, then throws, so
    // the comment and its mention rows must roll back together.
    [TestFixture]
    public class WhenSaveFailsAfterTheMentionInsert : ApiFixture
    {
        private const string Body = "@Mei Lin this save fails";

        protected override Action<IServiceCollection>? ConfigureTestServices => services =>
            services.AddScoped<IMentionRecorder, RecordThenThrowRecorder>();

        [Test]
        public async Task AddComment_WhenSaveFailsAfterMentionInsert_ShouldLeaveNoMentionRow()
        {
            await using var connection = await OpenDatabaseAsync();
            var before = await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM comment_mention WHERE user_id = @mei", new { mei = SeedIds.Users.Mei });

            using var response = await Api.PostJsonAsync(CommentsUrl, new CommentCreate(Body));

            response.StatusCode.ShouldBe(HttpStatusCode.InternalServerError);
            (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM comment_mention WHERE user_id = @mei", new { mei = SeedIds.Users.Mei }))
                .ShouldBe(before);
            (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM comment WHERE body = @Body", new { Body })).ShouldBe(0);
        }
    }

    private sealed class RecordThenThrowRecorder(TimeProvider clock) : IMentionRecorder
    {
        private readonly MentionRecorder _real = new(clock);

        public async Task RecordAsync(
            DbTransaction transaction, Guid commentId, Guid workItemId, string body, Guid? authorUserId, string? agentName,
            CancellationToken cancellationToken = default)
        {
            await _real.RecordAsync(transaction, commentId, workItemId, body, authorUserId, agentName, cancellationToken);
            var recorded = await transaction.Connection!.ExecuteScalarAsync<int>(new CommandDefinition(
                "SELECT count(*)::int FROM comment_mention WHERE comment_id = @commentId", new { commentId }, transaction, cancellationToken: cancellationToken));
            recorded.ShouldBe(1, "the double must fail after the mention row is written");
            throw new InvalidOperationException("The save failed after the mention was recorded.");
        }

        public Task ReconcileAsync(
            DbTransaction transaction, Guid commentId, Guid workItemId, string newBody, Guid? authorUserId, string? agentName,
            CancellationToken cancellationToken = default)
            => _real.ReconcileAsync(transaction, commentId, workItemId, newBody, authorUserId, agentName, cancellationToken);
    }
}
