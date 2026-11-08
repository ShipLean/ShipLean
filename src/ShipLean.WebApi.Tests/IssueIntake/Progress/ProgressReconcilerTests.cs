using System.Globalization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Application.Features.IssueIntake.Progress;
using ShipLean.Application.Features.IssueIntake.ShippedIn;
using ShipLean.Contracts.IssueIntake;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.IssueIntake;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.IssueIntake.Progress;

// The progress reconcile loop (D13, D14, D15, D17): milestones worked out from stored state, posted once each through the outbox,
// then the fixed-in or removed message and the close. Each test has its own project and repository; most reconcile their one issue.
[TestFixture]
public class ProgressReconcilerTests : ApiFixture
{
    private const string Instance = "https://shiplean.example.test";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<FixedIssueIntakeSettings>();
        services.AddScoped<IIssueIntakeSettings>(sp => sp.GetRequiredService<FixedIssueIntakeSettings>());
        services.AddSingleton<IGitHubOutbox, DbGitHubOutbox>();
        services.AddSingleton<FixedImagePublications>();
        services.AddSingleton<HookedImagePublications>();
        services.AddSingleton<IImagePublications>(sp => sp.GetRequiredService<HookedImagePublications>());
    };

    private ProgressReconciler Reconciler => Host.Services.GetRequiredService<ProgressReconciler>();

    private IGitHubIssueStore Issues => Host.Services.GetRequiredService<IGitHubIssueStore>();

    private DateTimeOffset Now => Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();

    [SetUp]
    public async Task BaseUrlAsync()
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE instance_settings SET base_url = @Instance", new { Instance });
    }

    [Test]
    public async Task Reconcile_WhenSprintSetTwice_ShouldPostOnce()
    {
        var arranged = await ArrangeAsync();
        var sprintId = await AddSprintAsync(arranged, "Sprint 4");
        await SetSprintAsync(arranged.ItemId, sprintId);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        await SetSprintAsync(arranged.ItemId, sprintId);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var posted = (await OutboxAsync(arranged.Issue.Id)).ShouldHaveSingleItem();
        var milestone = ProgressReconciler.SprintMilestone(sprintId);
        posted.DedupeKey.ShouldBe(ProgressReconciler.DedupeKey(arranged.Issue.Id, milestone));
        posted.Body.ShouldNotBeNull().ShouldStartWith($"{arranged.Key} \"Add the thing\" is planned for the sprint Sprint 4 (");
        (await IssueAsync(arranged.Issue.Id)).LastPosted[milestone].ShouldBe(posted.DedupeKey);
    }

    [Test]
    public async Task Reconcile_WhenRestartedAfterChange_ShouldStillPost()
    {
        var arranged = await ArrangeAsync();
        await MoveAsync(arranged, "inProgress");

        // A reconciler built after the change, as after a restart: no event told it anything.
        var restarted = ActivatorUtilities.CreateInstance<ProgressReconciler>(Host.Services);
        await restarted.RunPassAsync(CancellationToken.None);

        var posted = (await OutboxAsync(arranged.Issue.Id)).ShouldHaveSingleItem();
        posted.DedupeKey.ShouldBe(ProgressReconciler.DedupeKey(arranged.Issue.Id, ProgressReconciler.Started));
        posted.Body.ShouldBe($"Work on {arranged.Key} \"Add the thing\" has started.");
    }

    [Test]
    public async Task Done_WhenPublished_ShouldPostFixedInAndClose()
    {
        var arranged = await ArrangeAsync(requires: "The webhook secret is set");
        var pull = await AddPullAsync(arranged, "merged");
        var publishedAt = Minute(Now);
        var runUrl = "https://github.com/shiplean-tests/progress/actions/runs/42";
        Host.Services.GetRequiredService<FixedImagePublications>().Set(pull.Sha, new ImagePublication.Published(publishedAt, runUrl, pull.Sha));
        await MoveAsync(arranged, "completed");

        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var outbox = await OutboxAsync(arranged.Issue.Id);
        outbox.Count.ShouldBe(2);
        outbox[0].DedupeKey.ShouldBe(ProgressReconciler.DedupeKey(arranged.Issue.Id, ProgressReconciler.Done));
        var body = outbox[0].Body.ShouldNotBeNull();
        var published = publishedAt.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        body.ShouldStartWith($"{arranged.Key} \"Add the thing\" is done.\n\n{ShippedInSection.Heading}\n\n- {arranged.Key}: commit `{pull.Sha[..7]}` (shiplean-tests/progress#{pull.Number}), merged ");
        body.ShouldContain($"Image published {published} (run: {runUrl}).");
        body.ShouldContain($"`git merge-base --is-ancestor {pull.Sha[..7]} <sha>`");
        body.ShouldContain("It works only when:\n- The webhook secret is set");
        body.ShouldEndWith(ProgressMessages.ClosingCompleted);
        outbox[1].Kind.ShouldBe("close");
        outbox[1].Reason.ShouldBe("completed");

        // The close is only enqueued: the phase moves to closed once it was sent (Close_WhenSent_ShouldMovePhaseToClosed).
        (await IssueAsync(arranged.Issue.Id)).Phase.ShouldBe(IssuePhase.Accepted);
    }

    [Test]
    public async Task Done_WhenPending_ShouldWait()
    {
        var arranged = await ArrangeAsync();
        var pull = await AddPullAsync(arranged, "merged");
        Host.Services.GetRequiredService<FixedImagePublications>().Set(pull.Sha, new ImagePublication.Pending());
        await MoveAsync(arranged, "completed");

        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        (await OutboxAsync(arranged.Issue.Id)).ShouldBeEmpty();
        var waiting = await IssueAsync(arranged.Issue.Id);
        waiting.Phase.ShouldBe(IssuePhase.Accepted);
        waiting.LastPosted.ShouldContainKey(ProgressReconciler.DoneSeen);

        await Issues.MarkPostedAsync(arranged.Issue.Id, ProgressReconciler.DoneSeen, (Now - ProgressReconciler.PublishWait).AddMinutes(-1).ToString("O", CultureInfo.InvariantCulture));
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var outbox = await OutboxAsync(arranged.Issue.Id);
        outbox.Select(o => o.Kind).ShouldBe(["comment", "close"]);
        outbox[0].Body.ShouldNotBeNull().ShouldContain(ShippedInSection.NotYetPublished);
        (await IssueAsync(arranged.Issue.Id)).Phase.ShouldBe(IssuePhase.Accepted);
    }

    [Test]
    public async Task Done_WhenBuildFailed_ShouldSaySo()
    {
        var arranged = await ArrangeAsync();
        var pull = await AddPullAsync(arranged, "merged");
        var runUrl = "https://github.com/shiplean-tests/progress/actions/runs/43";
        Host.Services.GetRequiredService<FixedImagePublications>().Set(pull.Sha, new ImagePublication.Failed(runUrl));
        await MoveAsync(arranged, "completed");

        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var outbox = await OutboxAsync(arranged.Issue.Id);
        outbox.Select(o => o.Kind).ShouldBe(["comment", "close"]);
        outbox[0].Body.ShouldNotBeNull().ShouldContain($"Image: not yet published; the publish run failed ({runUrl}).");
    }

    [Test]
    public async Task Done_WhenNoMergedPr_ShouldNotInventCommit()
    {
        var arranged = await ArrangeAsync();
        await AddPullAsync(arranged, "closed");
        await MoveAsync(arranged, "completed");

        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var outbox = await OutboxAsync(arranged.Issue.Id);
        outbox.Select(o => o.Kind).ShouldBe(["comment", "close"]);
        var body = outbox[0].Body.ShouldNotBeNull();
        body.ShouldContain(ShippedInSection.Unconfirmed);
        body.ShouldNotContain("commit `");
        body.ShouldNotContain("merge-base");
    }

    [Test]
    public async Task ChildCompleted_ShouldPostCount()
    {
        var arranged = await ArrangeAsync(type: "feature");
        var children = new List<(Guid Id, string Key)>();
        for (var i = 0; i < 3; i++)
        {
            var (id, key, _) = await arranged.Dispatch.CreateItemAsync(Api);
            children.Add((id, key));
            await using var database = await OpenDatabaseAsync();
            await database.ExecuteAsync("UPDATE work_item SET parent_id = @parent, parent_type = 'feature' WHERE id = @id", new { parent = arranged.ItemId, id });
        }

        await SetCategoryAsync(arranged, children[1].Id, "story", "completed");
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var posted = (await OutboxAsync(arranged.Issue.Id)).ShouldHaveSingleItem();
        posted.DedupeKey.ShouldBe(ProgressReconciler.DedupeKey(arranged.Issue.Id, ProgressReconciler.ChildMilestone(children[1].Id)));
        posted.Body.ShouldBe($"{children[1].Key} \"Add the thing\" is done: 1 of 3 done for {arranged.Key} \"Add the thing\".");
    }

    [Test]
    public async Task Removed_ShouldCloseAsNotPlanned()
    {
        var arranged = await ArrangeAsync();
        await MoveAsync(arranged, "removed");

        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var outbox = await OutboxAsync(arranged.Issue.Id);
        outbox.Select(o => o.Kind).ShouldBe(["comment", "close"]);
        outbox[0].Body.ShouldBe(ProgressMessages.Removed(arranged.Key, "Add the thing"));
        outbox[1].Reason.ShouldBe("not_planned");
        (await IssueAsync(arranged.Issue.Id)).Phase.ShouldBe(IssuePhase.Accepted);
    }

    [Test]
    public async Task Reconcile_WhenIssueClosedOnGitHub_ShouldPostNothing()
    {
        var arranged = await ArrangeAsync();
        (await Issues.SetOpenOnGitHubAsync(arranged.Issue.Id, false, Now)).ShouldBeTrue();
        await MoveAsync(arranged, "removed");

        await Reconciler.RunPassAsync(CancellationToken.None);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        (await OutboxAsync(arranged.Issue.Id)).ShouldBeEmpty();
        (await IssueAsync(arranged.Issue.Id)).Phase.ShouldBe(IssuePhase.Accepted);
    }

    [Test]
    public async Task Reconcile_WhenAccepting_ShouldPostNothing()
    {
        var arranged = await ArrangeAsync(phase: IssuePhase.Accepting);
        await MoveAsync(arranged, "inProgress");

        await Reconciler.RunPassAsync(CancellationToken.None);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        (await OutboxAsync(arranged.Issue.Id)).ShouldBeEmpty();
    }

    [Test]
    public async Task Reconcile_WhenNoBaseUrl_ShouldHoldOnceAndPostNothing()
    {
        var arranged = await ArrangeAsync();
        await MoveAsync(arranged, "removed");
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE instance_settings SET base_url = ''");
        }

        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        (await OutboxAsync(arranged.Issue.Id)).ShouldBeEmpty();
        var held = await IssueAsync(arranged.Issue.Id);
        held.Phase.ShouldBe(IssuePhase.Accepted);
        held.LastPosted[ProgressReconciler.HeldKey(ProgressReconciler.Removed)].ShouldBe(ProgressReconciler.NoBaseUrl);
        (await ShipLeanCommentsAsync(arranged.ItemId)).ShouldHaveSingleItem().ShouldContain("Set the instance base URL");
    }

    [Test]
    public async Task Done_WhenItemReopenedWhileReading_ShouldPostNothing()
    {
        var arranged = await ArrangeAsync();
        var pull = await AddPullAsync(arranged, "merged");
        Host.Services.GetRequiredService<FixedImagePublications>().Set(pull.Sha, new ImagePublication.NotApplicable());
        await MoveAsync(arranged, "completed");

        // The facts are read before the transaction; the item is reopened right then, before the lock is taken.
        Host.Services.GetRequiredService<HookedImagePublications>().Once(() => MoveAsync(arranged, "inProgress"));
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        (await OutboxAsync(arranged.Issue.Id)).ShouldBeEmpty();
        var issue = await IssueAsync(arranged.Issue.Id);
        issue.Phase.ShouldBe(IssuePhase.Accepted);
        issue.LastPosted.ShouldNotContainKey(ProgressReconciler.Done);
    }

    [Test]
    public async Task Close_WhenItemReopenedBeforeDelivery_ShouldNotSendClose()
    {
        var arranged = await ArrangeAsync();
        await MoveAsync(arranged, "completed");
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        (await OutboxAsync(arranged.Issue.Id)).Select(o => o.Kind).ShouldBe(["comment", "close"]);

        await MoveAsync(arranged, "inProgress");
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        // Neither the pending close nor the pending done message goes out.
        var doneKey = ProgressReconciler.DedupeKey(arranged.Issue.Id, ProgressReconciler.Done);
        var closeKey = ProgressReconciler.DedupeKey(arranged.Issue.Id, ProgressReconciler.Close);
        var cancelled = (await OutboxAsync(arranged.Issue.Id)).Where(o => o.DedupeKey == doneKey || o.DedupeKey == closeKey).ToList();
        cancelled.Count.ShouldBe(2);
        cancelled.ShouldAllBe(o => o.SentAt != null && o.LastResult == ProgressReconciler.SkippedItemReopened);
        var reopened = await IssueAsync(arranged.Issue.Id);
        reopened.Phase.ShouldBe(IssuePhase.Accepted);
        reopened.LastPosted.ShouldNotContainKey(ProgressReconciler.Done);
        reopened.LastPosted.ShouldNotContainKey(ProgressReconciler.Close);

        // Done again: the done message and the close are due again, under fresh dedupe keys.
        await MoveAsync(arranged, "completed");
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        var due = (await OutboxAsync(arranged.Issue.Id))
            .Where(o => o.SentAt is null && (o.DedupeKey.StartsWith(doneKey, StringComparison.Ordinal) || o.DedupeKey.StartsWith(closeKey, StringComparison.Ordinal)));
        due.Select(o => o.DedupeKey).ShouldBe([doneKey + ":r1", closeKey + ":r1"]);
    }

    [Test]
    public async Task Close_WhenIssueClosedBeforeDeliveryThenReopened_ShouldPostDoneAndCloseOnce()
    {
        var arranged = await ArrangeAsync();
        await MoveAsync(arranged, "completed");
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        // GitHub closes the issue before delivery, so the deliverer finishes both entries unsent.
        (await Issues.SetOpenOnGitHubAsync(arranged.Issue.Id, false, Now)).ShouldBeTrue();
        await FinishAllAsync(arranged.Issue.Id, "skipped: issue closed");
        await Reconciler.RunPassAsync(CancellationToken.None);
        (await OutboxAsync(arranged.Issue.Id)).Where(o => o.SentAt is null).ShouldBeEmpty();
        (await IssueAsync(arranged.Issue.Id)).Phase.ShouldBe(IssuePhase.Accepted);

        (await Issues.SetOpenOnGitHubAsync(arranged.Issue.Id, true, Now.AddSeconds(1))).ShouldBeTrue();
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        await FinishAllAsync(arranged.Issue.Id, "ok");
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var sent = (await OutboxAsync(arranged.Issue.Id)).Where(o => o.LastResult == "ok").ToList();
        sent.Select(o => o.Kind).ShouldBe(["comment", "close"]);
        sent[0].Body.ShouldNotBeNull().ShouldStartWith($"{arranged.Key} \"Add the thing\" is done.");
        sent.Select(o => o.DedupeKey).ShouldAllBe(k => k.EndsWith(":r1", StringComparison.Ordinal));
        (await IssueAsync(arranged.Issue.Id)).Phase.ShouldBe(IssuePhase.Closed);
    }

    [Test]
    public async Task Close_WhenSent_ShouldMovePhaseToClosed()
    {
        var arranged = await ArrangeAsync();
        await MoveAsync(arranged, "removed");
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        await FinishAsync(arranged.Issue.Id, "comment", "ok");
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        (await IssueAsync(arranged.Issue.Id)).Phase.ShouldBe(IssuePhase.Accepted);

        // The close went out and GitHub's closed event came back, so the issue is no longer open: the pass still finishes it.
        await FinishAsync(arranged.Issue.Id, "close", "ok");
        (await Issues.SetOpenOnGitHubAsync(arranged.Issue.Id, false, Now)).ShouldBeTrue();
        await Reconciler.RunPassAsync(CancellationToken.None);

        (await IssueAsync(arranged.Issue.Id)).Phase.ShouldBe(IssuePhase.Closed);
        (await OutboxAsync(arranged.Issue.Id)).Count.ShouldBe(2);
    }

    [Test]
    public async Task Done_WhenFeatureHasTwelveChildren_ShouldFindEveryChildsCommit()
    {
        var arranged = await ArrangeAsync(type: "feature");
        var children = await AddChildrenAsync(arranged, 12);
        var pull = await AddPullAsync(arranged, "merged", children[11].Id);
        Host.Services.GetRequiredService<FixedImagePublications>().Set(pull.Sha, new ImagePublication.NotApplicable());
        await MoveAsync(arranged, "completed");

        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var done = (await OutboxAsync(arranged.Issue.Id)).First(o => o.Kind == "comment");
        done.Body.ShouldNotBeNull().ShouldContain($"- {children[11].Key}: commit `{pull.Sha[..7]}`");
    }

    [Test]
    public async Task Done_WhenMergeCommitNotReported_ShouldWaitFiveMinutes()
    {
        var arranged = await ArrangeAsync();
        await AddPullAsync(arranged, "merged", withoutSha: true);
        await MoveAsync(arranged, "completed");

        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        (await OutboxAsync(arranged.Issue.Id)).ShouldBeEmpty();
        (await IssueAsync(arranged.Issue.Id)).LastPosted.ShouldContainKey(ProgressReconciler.DoneSeen);

        await Issues.MarkPostedAsync(arranged.Issue.Id, ProgressReconciler.DoneSeen, Now.AddMinutes(-4).ToString("O", CultureInfo.InvariantCulture));
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        (await OutboxAsync(arranged.Issue.Id)).ShouldBeEmpty();

        await Issues.MarkPostedAsync(arranged.Issue.Id, ProgressReconciler.DoneSeen, (Now - ProgressReconciler.MergeShaWait).AddMinutes(-1).ToString("O", CultureInfo.InvariantCulture));
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);
        var outbox = await OutboxAsync(arranged.Issue.Id);
        outbox.Select(o => o.Kind).ShouldBe(["comment", "close"]);
        outbox[0].Body.ShouldNotBeNull().ShouldContain(ShippedInSection.Unconfirmed);
    }

    // A pull request posts its current link_state only: one opened and merged between two passes posts "merged" alone.
    [Test]
    public async Task PullRequest_WhenOpenedAndMergedInOnePass_ShouldPostMergedOnly()
    {
        var arranged = await ArrangeAsync();
        var pull = await AddPullAsync(arranged, "merged");

        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var posted = (await OutboxAsync(arranged.Issue.Id)).ShouldHaveSingleItem();
        posted.DedupeKey.ShouldBe(ProgressReconciler.DedupeKey(
            arranged.Issue.Id, ProgressReconciler.PullMilestone($"github.com/shiplean-tests/progress/pull/{pull.Number}", "merged")));
    }

    [Test]
    public async Task Reconcile_WhenSprintChanged_ShouldPostNewSprint()
    {
        var arranged = await ArrangeAsync();
        var first = await AddSprintAsync(arranged, "Sprint 4");
        var second = await AddSprintAsync(arranged, "Sprint 5");
        await SetSprintAsync(arranged.ItemId, first);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        await SetSprintAsync(arranged.ItemId, second);
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var outbox = await OutboxAsync(arranged.Issue.Id);
        outbox.Select(o => o.DedupeKey).ShouldBe([
            ProgressReconciler.DedupeKey(arranged.Issue.Id, ProgressReconciler.SprintMilestone(first)),
            ProgressReconciler.DedupeKey(arranged.Issue.Id, ProgressReconciler.SprintMilestone(second))]);
        outbox[1].Body.ShouldNotBeNull().ShouldContain("Sprint 5");
    }

    [Test]
    public async Task Reconcile_WhenSprintAndChildTitlesHoldEmails_ShouldPostThemGuarded()
    {
        var arranged = await ArrangeAsync(type: "feature");
        var sprintId = await AddSprintAsync(arranged, "Sprint for ann@example.com");
        await SetSprintAsync(arranged.ItemId, sprintId);
        var child = (await AddChildrenAsync(arranged, 1))[0];
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE work_item SET title = 'Ask bob@example.com' WHERE id = @id", new { id = child.Id });
        }

        await SetCategoryAsync(arranged, child.Id, "story", "completed");
        await Reconciler.ReconcileAsync(arranged.Issue.Id, CancellationToken.None);

        var outbox = await OutboxAsync(arranged.Issue.Id);
        outbox.Select(o => o.DedupeKey).ShouldBe([
            ProgressReconciler.DedupeKey(arranged.Issue.Id, ProgressReconciler.SprintMilestone(sprintId)),
            ProgressReconciler.DedupeKey(arranged.Issue.Id, ProgressReconciler.ChildMilestone(child.Id))]);
        outbox.ShouldAllBe(o => !o.Body!.Contains("example.com"));
        outbox[1].Body.ShouldNotBeNull().ShouldContain($"{child.Key} \"Ask");
    }

    private static DateTimeOffset Minute(DateTimeOffset at) => at.AddTicks(-(at.Ticks % TimeSpan.TicksPerMinute));

    private async Task<Arranged> ArrangeAsync(string type = "story", IssuePhase phase = IssuePhase.Accepted, string? requires = null)
    {
        AgentDispatchArrange dispatch;
        await using (var database = await OpenDatabaseAsync())
        {
            dispatch = await AgentDispatchArrange.CreateAsync(database, Api);
        }

        var (item, key, _) = await dispatch.CreateItemAsync(Api, type: type);
        var repository = $"shiplean-tests/progress-{Guid.NewGuid():N}";
        var issue = await Issues.InsertAsync(
            new IssueRef(IssueRef.GitHubHost, repository, 1), dispatch.Project.Id, item, "reporter", "Please add the thing", "The issue body.", phase);
        await using (var database = await OpenDatabaseAsync())
        {
            if (requires is not null)
            {
                // The accepted proposal as the inbox stores it; no agent session is needed to read its requires.
                var proposal = $$"""{"verdict":"new","type":"story","title":"Add the thing","requires":["{{requires}}"]}""";
                await database.ExecuteAsync("UPDATE github_issue SET proposal = @proposal::jsonb WHERE id = @id", new { proposal, id = issue.Id });
            }

            var projectKey = await database.QuerySingleAsync<string>("SELECT key FROM project WHERE id = @id", new { id = dispatch.Project.Id });
            Host.Services.GetRequiredService<FixedIssueIntakeSettings>()
                .Take(new IssueIntakeProject(dispatch.Project.Id, projectKey, repository, SeedIds.Users.Joel, "Instructions.", 5));
        }

        return new Arranged(dispatch, item, key, type, issue);
    }

    private async Task MoveAsync(Arranged arranged, string category) => await SetCategoryAsync(arranged, arranged.ItemId, arranged.Type, category);

    private async Task SetCategoryAsync(Arranged arranged, Guid itemId, string type, string category)
    {
        await using var database = await OpenDatabaseAsync();
        var changed = await database.ExecuteAsync(
            """
            UPDATE work_item SET state_id = (
                SELECT id FROM state WHERE project_id = @projectId AND type = @type AND category = @category AND archived_at IS NULL ORDER BY sort_order LIMIT 1)
            WHERE id = @itemId
            """,
            new { projectId = arranged.Dispatch.Project.Id, type, category, itemId });
        changed.ShouldBe(1);
    }

    private async Task<Guid> AddSprintAsync(Arranged arranged, string name)
    {
        var id = Guid.NewGuid();
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @projectId, @name, current_date, current_date + 13)",
            new { id, projectId = arranged.Dispatch.Project.Id, name });
        return id;
    }

    private async Task SetSprintAsync(Guid itemId, Guid sprintId)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync("UPDATE work_item SET sprint_id = @sprintId WHERE id = @itemId", new { itemId, sprintId });
    }

    private async Task<List<(Guid Id, string Key)>> AddChildrenAsync(Arranged arranged, int count)
    {
        var children = new List<(Guid Id, string Key)>();
        for (var i = 0; i < count; i++)
        {
            var (id, key, _) = await arranged.Dispatch.CreateItemAsync(Api);
            children.Add((id, key));
            await using var database = await OpenDatabaseAsync();
            await database.ExecuteAsync("UPDATE work_item SET parent_id = @parent, parent_type = 'feature' WHERE id = @id", new { parent = arranged.ItemId, id });
        }

        return children;
    }

    // Finishes the issue's pending entries as the deliverer would: 'ok' when sent, or a skip.
    private async Task FinishAllAsync(Guid issueId, string result)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            "UPDATE github_outbox SET sent_at = now(), last_result = @result WHERE github_issue_id = @issueId AND sent_at IS NULL", new { issueId, result });
    }

    private async Task FinishAsync(Guid issueId, string kind, string result)
    {
        await using var database = await OpenDatabaseAsync();
        var finished = await database.ExecuteAsync(
            "UPDATE github_outbox SET sent_at = now(), last_result = @result WHERE github_issue_id = @issueId AND kind = @kind AND sent_at IS NULL",
            new { issueId, kind, result });
        finished.ShouldBe(1);
    }

    private async Task<(int Number, string Sha)> AddPullAsync(Arranged arranged, string state, Guid? itemId = null, bool withoutSha = false)
    {
        var number = Random.Shared.Next(1, int.MaxValue);
        var sha = Convert.ToHexStringLower(Guid.NewGuid().ToByteArray());
        var url = $"https://github.com/shiplean-tests/progress/pull/{number}";
        var merged = state == "merged";
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            """
            INSERT INTO work_item_link (id, source_id, link_type, url, title, created_by, created_at)
            VALUES (@linkId, @item, 'url', @url, 'Pull request', @joel, now())
            """,
            new { linkId = Guid.NewGuid(), item = itemId ?? arranged.ItemId, url, joel = SeedIds.Users.Joel });
        await database.ExecuteAsync(
            """
            INSERT INTO link_state (url_key, url, state, as_of, source, merge_commit_sha, merged_at)
            VALUES (@urlKey, @url, @state, now(), 'github', @sha, CASE WHEN @merged THEN now() END)
            """,
            new { urlKey = $"github.com/shiplean-tests/progress/pull/{number}", url, state, sha = merged && !withoutSha ? sha : null, merged });
        return (number, sha);
    }

    private async Task<GitHubIssueRecord> IssueAsync(Guid id) => (await Issues.FindByIdAsync(id)).ShouldNotBeNull();

    private async Task<List<OutboxRow>> OutboxAsync(Guid issueId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<OutboxRow>(
            "SELECT dedupe_key AS dedupeKey, kind, body, reason, sent_at AS sentAt, last_result AS lastResult FROM github_outbox WHERE github_issue_id = @issueId ORDER BY seq", new { issueId })];
    }

    private async Task<List<string>> ShipLeanCommentsAsync(Guid workItemId)
    {
        await using var database = await OpenDatabaseAsync();
        return [.. await database.QueryAsync<string>(
            "SELECT body FROM comment WHERE work_item_id = @workItemId AND agent_name = 'ShipLean' ORDER BY created_at", new { workItemId })];
    }

    private sealed record Arranged(AgentDispatchArrange Dispatch, Guid ItemId, string Key, string Type, GitHubIssueRecord Issue);

    private sealed record OutboxRow(string DedupeKey, string Kind, string? Body, string? Reason, DateTimeOffset? SentAt, string? LastResult);

    // FixedImagePublications with a one-time action before an answer: lets a test change the item while the facts are read.
    private sealed class HookedImagePublications(FixedImagePublications answers) : IImagePublications
    {
        private Func<Task>? _once;

        public void Once(Func<Task> action)
        {
            _once = action;
        }

        public async Task<ImagePublication> FindAsync(IssueRef repository, string sha, CancellationToken cancellationToken = default)
        {
            var action = Interlocked.Exchange(ref _once, null);
            if (action is not null)
            {
                await action();
            }

            return await answers.FindAsync(repository, sha, cancellationToken);
        }
    }
}
