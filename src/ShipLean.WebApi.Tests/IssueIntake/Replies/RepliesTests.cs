using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Webhooks;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.IssueIntake;

namespace ShipLean.WebApi.Tests.IssueIntake.Replies;

// DbIssueReplies on the real stores: every comment is mirrored once per GitHub URL, and a reply from the author or a
// maintainer starts one round while the conversation waits for one. Each test takes its own issue number.
[TestFixture]
public class RepliesTests : ApiFixture
{
    private const string Repository = "shiplean-tests/replies";

    private static int _number = 5000;

    private readonly Dictionary<Guid, int> _baseline = [];
    private readonly RecordingProposalSessions _rounds = new();
    private readonly FixedIssueIntakeSettings _settings = new FixedIssueIntakeSettings()
        .Take(new IssueIntakeProject(SeedIds.Projects.Ship, "SHIP", Repository, SeedIds.Users.Joel, "Write a proposal.", 5));

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IIssueIntakeSettings>(_settings);
        services.AddSingleton<IIssueProposalSessions>(_rounds);
    };

    [SetUp]
    public async Task ArrangeAsync()
    {
        _rounds.Next = RoundRequest.Requested;
        await using var connection = await OpenDatabaseAsync();
        foreach (var type in new[] { "story", "bug" })
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO state (id, project_id, type, name, category, sort_order, is_intake)
                SELECT @id, @project, @type, 'Triage', 'proposed', -1, true
                WHERE NOT EXISTS (SELECT 1 FROM state WHERE project_id = @project AND type = @type AND is_intake)
                """,
                new { id = Guid.NewGuid(), project = SeedIds.Projects.Ship, type });
        }
    }

    [Test]
    public async Task Reply_WhenAwaitingReply_ShouldMirrorAndStartRound()
    {
        var record = await TakenAsync(IssuePhase.AwaitingReply);
        var comment = Comment(record, 1, "Could it filter by Feature too?");

        var outcome = await ReplyAsync(comment);

        outcome.Outcome.ShouldBe(WebhookOutcome.Processed);
        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
        (await PhaseAsync(record)).ShouldBe(IssuePhase.Proposing);
        RoundsFor(record).ShouldBe(1);
    }

    [TestCase(IssuePhase.AlreadyShipped)]
    [TestCase(IssuePhase.WaitingForMaintainer)]
    public async Task Reply_WhenWaitingPhase_ShouldStartRound(IssuePhase phase)
    {
        var record = await TakenAsync(phase);

        await ReplyAsync(Comment(record, 1, "More detail"));

        (await PhaseAsync(record)).ShouldBe(IssuePhase.Proposing);
        RoundsFor(record).ShouldBe(1);
    }

    [Test]
    public async Task Reply_WhenProposing_ShouldOnlyMirror()
    {
        var record = await TakenAsync(IssuePhase.Proposing);
        var comment = Comment(record, 1, "Also this");

        await ReplyAsync(comment);

        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
        RoundsFor(record).ShouldBe(0);
        (await PhaseAsync(record)).ShouldBe(IssuePhase.Proposing);
    }

    [Test]
    public async Task Reply_WhenConcurrentTwo_ShouldRequestOneRound()
    {
        var record = await TakenAsync(IssuePhase.AwaitingReply);
        var first = Comment(record, 1, "One");
        var second = Comment(record, 2, "Two");

        await Task.WhenAll(ReplyAsync(first), ReplyAsync(second));

        RoundsFor(record).ShouldBe(1);
        (await MirroredAsync(first.CommentUrl)).ShouldBe(1);
        (await MirroredAsync(second.CommentUrl)).ShouldBe(1);
    }

    [Test]
    public async Task Reply_WhenReplayedAfterRoundFinished_ShouldNotStartAnotherRound()
    {
        var record = await TakenAsync(IssuePhase.AwaitingReply);
        var comment = Comment(record, 1, "Again");
        await ReplyAsync(comment);
        await SetPhaseAsync(record, IssuePhase.Proposing, IssuePhase.AwaitingReply);

        await ReplyAsync(comment);

        RoundsFor(record).ShouldBe(1);
        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
    }

    [Test]
    public async Task Reply_WhenRoundHeld_ShouldCommitTheRequest()
    {
        var record = await TakenAsync(IssuePhase.AwaitingReply);
        _rounds.Next = RoundRequest.Held("roundCap");

        var outcome = await ReplyAsync(Comment(record, 1, "Hello"));

        outcome.Outcome.ShouldBe(WebhookOutcome.Processed);
        RoundsFor(record).ShouldBe(1);
        (await PhaseAsync(record)).ShouldBe(IssuePhase.Proposing);
    }

    [Test]
    public async Task Reply_WhenIntakeOff_ShouldMirrorAndLeavePhase()
    {
        var record = await TakenAsync(IssuePhase.AwaitingReply);
        _rounds.Next = RoundRequest.Held("intakeOff");
        var comment = Comment(record, 1, "Hello");

        await ReplyAsync(comment);

        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
        (await PhaseAsync(record)).ShouldBe(IssuePhase.AwaitingReply);
    }

    [Test]
    public async Task Reply_WhenNoRecord_ShouldIgnore()
    {
        var unknown = new IssueRef(IssueRef.GitHubHost, Repository, Interlocked.Increment(ref _number));

        (await ReplyAsync(Comment(unknown, 1, "Hi"))).ShouldBe(IssueOutcome.Ignored);
    }

    [Test]
    public async Task Mirror_WhenReplayed_ShouldAddOnce()
    {
        var record = await TakenAsync(IssuePhase.AwaitingReply);
        var comment = Comment(record, 1, "Me too");

        await MirrorAsync(comment);
        await MirrorAsync(comment);

        (await MirroredAsync(comment.CommentUrl)).ShouldBe(1);
        (await CommentBodiesAsync(record)).Count(b => b.Contains("Me too", StringComparison.Ordinal)).ShouldBe(1);
    }

    [Test]
    public async Task Mirror_ShouldNotStartRound()
    {
        var record = await TakenAsync(IssuePhase.AwaitingReply);

        await MirrorAsync(Comment(record, 1, "Passer-by"));

        RoundsFor(record).ShouldBe(0);
        (await PhaseAsync(record)).ShouldBe(IssuePhase.AwaitingReply);
    }

    [Test]
    public async Task Mirror_WhenBodyTooLong_ShouldCapIt()
    {
        var record = await TakenAsync(IssuePhase.AwaitingReply);
        var comment = Comment(record, 1, new string('x', IssueEvent.MaxTextBytes + 10));

        await MirrorAsync(comment);

        await using var connection = await OpenDatabaseAsync();
        var stored = await connection.ExecuteScalarAsync<string>("SELECT body FROM github_issue_comment WHERE html_url = @url", new { url = comment.CommentUrl });
        stored!.ShouldEndWith(IssueEvent.TruncatedNote);
    }

    private static IssueCommentEvent Comment(IssueRef issue, long id, string body)
        => new(issue, $"https://github.com/{issue.Repository}/issues/{issue.Number}", "Title", "reporter", true, false, id,
            $"https://github.com/{issue.Repository}/issues/{issue.Number}#issuecomment-{id}", body, "reporter", "NONE", false, DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));

    private static IssueCommentEvent Comment(GitHubIssueRecord record, long id, string body) => Comment(record.Issue, id, body);

    private int RoundsFor(GitHubIssueRecord record) => _rounds.Requests.Count(r => r.IssueId == record.Id) - _baseline[record.Id];

    private async Task<GitHubIssueRecord> TakenAsync(IssuePhase phase)
    {
        var number = Interlocked.Increment(ref _number);
        var issue = new IssueEvent(
            IssueAction.Opened, new IssueRef(IssueRef.GitHubHost, Repository, number), $"https://github.com/{Repository}/issues/{number}", $"Replies issue {number}", "Body",
            "reporter", "NONE", [], true, null, "reporter", false, DateTimeOffset.FromUnixTimeSeconds(1_800_000_000));
        await using var scope = Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IIssueIntake>().OpenedAsync(issue, CancellationToken.None);
        var store = scope.ServiceProvider.GetRequiredService<IGitHubIssueStore>();
        var record = (await store.FindAsync(issue.Issue))!;
        await store.SetPhaseAsync(record.Id, [IssuePhase.Proposing], phase);
        _baseline[record.Id] = _rounds.Requests.Count(r => r.IssueId == record.Id);
        return (await store.FindAsync(issue.Issue))!;
    }

    private async Task<IssueOutcome> ReplyAsync(IssueCommentEvent comment)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIssueReplies>().ReplyAsync(comment, CancellationToken.None);
    }

    private async Task<IssueOutcome> MirrorAsync(IssueCommentEvent comment)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<IIssueReplies>().MirrorAsync(comment, CancellationToken.None);
    }

    private async Task<IssuePhase> PhaseAsync(GitHubIssueRecord record)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        return (await scope.ServiceProvider.GetRequiredService<IGitHubIssueStore>().FindByIdAsync(record.Id))!.Phase;
    }

    private async Task SetPhaseAsync(GitHubIssueRecord record, IssuePhase from, IssuePhase to)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IGitHubIssueStore>().SetPhaseAsync(record.Id, [from], to);
    }

    private async Task<int> MirroredAsync(string url)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM github_issue_comment WHERE html_url = @url", new { url });
    }

    private async Task<IReadOnlyList<string>> CommentBodiesAsync(GitHubIssueRecord record)
    {
        await using var connection = await OpenDatabaseAsync();
        return [.. await connection.QueryAsync<string>("SELECT body FROM comment WHERE work_item_id = @id", new { id = record.WorkItemId })];
    }
}
