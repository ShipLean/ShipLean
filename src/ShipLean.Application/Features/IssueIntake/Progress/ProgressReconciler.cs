using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.IssueIntake.Outbox;
using ShipLean.Application.Features.IssueIntake.Proposal;
using ShipLean.Application.Features.IssueIntake.Rounds;
using ShipLean.Application.Features.IssueIntake.ShippedIn;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Progress;

// Progress on an accepted issue (D13, D14), worked out from stored state, never from events alone, so a restart loses nothing.
// For each issue ListAcceptedOpenAsync returns (phase accepted, not accepting; open on GitHub), it reads the item (state category,
// sprint), its children and its github.com pull request links with their link_state, compares them with github_issue.last_posted
// and enqueues the milestones not posted yet: sprint:<id>, started, pr:<url key>:<open|merged>, child:<id> ("n of m done", for a
// Feature), then done (the fixed-in message, then Close completed) or removed (the message, then Close not planned). A pull
// request posts its current link_state only, so one opened and merged between two passes posts "merged" alone.
// Each post is an outbox entry whose dedupe key is also stored in last_posted under its milestone, in one transaction under a
// per-issue lock, so a replayed or concurrent pass never posts twice. The item's state is read again under that lock: a pass
// whose earlier read no longer holds (the item moved, or the issue points at another item) enqueues nothing.
// A milestone counts as posted only once its entry was sent: an entry the deliverer skipped because the issue was closed is
// enqueued again as <dedupe>:r<n> once the issue is open. The phase stays accepted while the close is pending and moves to closed
// on a later pass, once the close entry was sent. If the item stops being done or removed while its close is still pending, the
// close is cancelled (sent_at, 'skipped: item reopened') under the outbox lock and the done, removed and close keys are cleared,
// so the next done posts again; a close already sent is left for a maintainer (D19).
// The done message carries ShippedInSection over ShippedInFacts (the item's and its children's merged pull requests, looked up
// MaxKeys at a time) and the proposal's requires. While a commit's image publication is Pending or Unknown it waits up to 2 h
// from when the pass first saw the item done (last_posted done:seen), and while a merged pull request has no merge commit yet
// it waits up to 5 min from then. Every text passes PublicTextGuard: an unsafe text, or no instance base URL (fail closed),
// posts nothing and holds the milestone for a maintainer with one ShipLean comment per refused text (last_posted
// held:<milestone>); the phase stays accepted so a later pass posts once the text passes, and a done or removed issue is not
// closed while its message is held.
public sealed class ProgressReconciler(
    IGitHubIssueStore issues,
    IGitHubOutbox outbox,
    ShippedInFacts shippedIn,
    IServiceScopeFactory scopes,
    NpgsqlDataSource dataSource,
    IProjectEvents events,
    TimeProvider time,
    ILogger<ProgressReconciler> logger)
{
    public const string DoneSeen = "done:seen";

    public const string Done = "done";

    public const string Removed = "removed";

    public const string Started = "started";

    public const string Close = "close";

    public const string NoBaseUrl = "noBaseUrl";

    public const string SkippedItemReopened = "skipped: item reopened";

    public static readonly TimeSpan PublishWait = TimeSpan.FromHours(2);

    // How long a merged pull request may go without its merge commit before the done message says it couldn't confirm the version.
    public static readonly TimeSpan MergeShaWait = TimeSpan.FromMinutes(5);

    private const string PullUrlKeyPattern = @"^github\.com/[^/]+/[^/]+/pull/[0-9]+$";

    public static string DedupeKey(Guid issueId, string milestone) => $"progress:{issueId}:{milestone}";

    public static string HeldKey(string milestone) => $"held:{milestone}";

    public static string SprintMilestone(Guid sprintId) => $"sprint:{sprintId}";

    public static string PullMilestone(string urlKey, string state) => $"pr:{urlKey}:{state}";

    public static string ChildMilestone(Guid childId) => $"child:{childId}";

    // One pass over every tracked issue, then over the accepted issues whose close was sent (those may be closed on GitHub by
    // now, so ListAcceptedOpenAsync no longer returns them). One issue that fails is logged and the others still run.
    public async Task RunPassAsync(CancellationToken cancellationToken)
    {
        var open = await issues.ListAcceptedOpenAsync(null, cancellationToken);
        var closing = await ClosesSentAsync(cancellationToken);
        foreach (var issueId in open.Select(i => i.Id).Concat(closing).Distinct())
        {
            try
            {
                await ReconcileAsync(issueId, cancellationToken);
            }
            catch (Exception failed) when (failed is not OperationCanceledException)
            {
                logger.LogError(failed, "Progress for github_issue {IssueId} failed", issueId);
            }
        }
    }

    // The projects whose intake is on: ProgressService wakes a pass early only for their events.
    public async Task<IReadOnlySet<Guid>> IntakeProjectsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return (await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT project_id FROM project_issue_intake WHERE enabled", cancellationToken: cancellationToken))).ToHashSet();
    }

    public async Task ReconcileAsync(Guid issueId, CancellationToken cancellationToken)
    {
        var found = await issues.FindByIdAsync(issueId, null, cancellationToken);
        if (found is not { Phase: IssuePhase.Accepted })
        {
            return;
        }

        // The item and the facts may call GitHub (IImagePublications), so they are read before the transaction and its lock.
        var read = found.OpenOnGitHub ? await ReadAsync(found, cancellationToken) : null;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('issue_progress:' || @issueId::text, 0))", new { issueId }, transaction, cancellationToken: cancellationToken));
        var issue = await issues.FindByIdAsync(issueId, transaction, cancellationToken);
        if (issue is not { Phase: IssuePhase.Accepted } || issue.WorkItemId != found.WorkItemId)
        {
            return;
        }

        var entries = await EntriesAsync(issueId, transaction, cancellationToken);
        if (Status(issue, Close, entries) == EntryStatus.Sent)
        {
            if (!await issues.SetPhaseAsync(issue.Id, [IssuePhase.Accepted], IssuePhase.Closed, transaction, cancellationToken))
            {
                throw new InvalidOperationException($"github_issue {issue.Id} left the accepted phase under the progress lock.");
            }

            await transaction.CommitAsync(cancellationToken);
            return;
        }

        if (read is null || !issue.OpenOnGitHub)
        {
            return;
        }

        // The item as it is now, under the lock: what was read before may no longer hold.
        var category = await CategoryAsync(issue.WorkItemId, transaction, cancellationToken);
        if (category is not ("completed" or "removed") && issue.LastPosted.ContainsKey(Close)
            && !await CancelCloseAsync(issue, entries, transaction, cancellationToken))
        {
            return;
        }

        var (runAs, item, facts) = read;
        if (category != item.Category)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }

        var baseUrls = await BaseUrlsAsync(transaction, cancellationToken);
        var guard = baseUrls.Count == 0 ? null : new PublicTextGuard(baseUrls, await MemberNamesAsync(issue.ProjectId, transaction, cancellationToken));
        var pass = new Pass(issue, guard, runAs, transaction, entries);

        switch (item.Category)
        {
            case "completed":
                await DoneAsync(pass, item, facts, cancellationToken);
                break;
            case "removed":
                if (await PostAsync(pass, Removed, ProgressMessages.Removed(item.Key, item.Title), cancellationToken))
                {
                    await CloseAsync(pass, CloseReason.NotPlanned, cancellationToken);
                }

                break;
            default:
                foreach (var (milestone, text) in Milestones(item))
                {
                    await PostAsync(pass, milestone, text, cancellationToken);
                }

                break;
        }

        await transaction.CommitAsync(cancellationToken);
        if (pass.Held)
        {
            await events.PublishAsync(new CommentsChanged(issue.ProjectId, issue.WorkItemId));
        }
    }

    // The milestones of an item still in progress, in posting order.
    private static IEnumerable<(string Milestone, string Text)> Milestones(ItemState item)
    {
        if (item.SprintId is { } sprintId)
        {
            yield return (SprintMilestone(sprintId), ProgressMessages.Sprint(item.Key, item.Title, item.SprintName!, item.SprintStart!, item.SprintEnd!));
        }

        if (item.Category is "inProgress" or "resolved")
        {
            yield return (Started, ProgressMessages.Started(item.Key, item.Title));
        }

        foreach (var pull in item.Pulls.Where(p => p.State is "open" or "merged"))
        {
            yield return (PullMilestone(pull.UrlKey, pull.State), ProgressMessages.PullRequest(item.Key, item.Title, $"https://{pull.UrlKey}", pull.State == "merged"));
        }

        if (item.Type != "feature")
        {
            yield break;
        }

        var counted = item.Children.Where(c => c.Category != "removed").ToList();
        var done = counted.Count(c => c.Category == "completed");
        foreach (var child in counted.Where(c => c.Category == "completed"))
        {
            yield return (ChildMilestone(child.Id), ProgressMessages.ChildDone(item.Key, item.Title, child.Key, child.Title, done, counted.Count));
        }
    }

    private async Task DoneAsync(Pass pass, ItemState item, IReadOnlyList<ShippedFact> facts, CancellationToken cancellationToken)
    {
        var wait = TimeSpan.Zero;
        if (facts.Any(f => f.Publication is ImagePublication.Pending or ImagePublication.Unknown))
        {
            wait = PublishWait;
        }
        else if (item.MergedWithoutSha)
        {
            wait = MergeShaWait;
        }

        if (wait > TimeSpan.Zero)
        {
            var now = time.GetUtcNow();
            if (!pass.Issue.LastPosted.TryGetValue(DoneSeen, out var seenText))
            {
                await issues.MarkPostedAsync(pass.Issue.Id, DoneSeen, now.ToString("O", CultureInfo.InvariantCulture), pass.Transaction, cancellationToken);
                return;
            }

            var seen = DateTimeOffset.Parse(seenText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (now - seen < wait)
            {
                return;
            }
        }

        var text = ProgressMessages.Done(item.Key, item.Title, ShippedInSection.Render(facts, Requires(pass.Issue.Proposal)));
        if (await PostAsync(pass, Done, text, cancellationToken))
        {
            await CloseAsync(pass, CloseReason.Completed, cancellationToken);
        }
    }

    // Enqueues the close after the done or removed comment (seq order delivers the comment first). The phase moves to closed on a
    // later pass, once this entry was sent, so a close that is still pending can be cancelled.
    private async Task CloseAsync(Pass pass, CloseReason reason, CancellationToken cancellationToken)
    {
        var status = Status(pass.Issue, Close, pass.Entries);
        if (status is EntryStatus.None or EntryStatus.SkippedClosed)
        {
            var key = NextKey(pass, Close);
            await outbox.EnqueueAsync(OutboxEntry.Close(pass.Issue.Id, key, reason), pass.Transaction, cancellationToken);
            await issues.MarkPostedAsync(pass.Issue.Id, Close, key, pass.Transaction, cancellationToken);
        }
    }

    // Enqueues one milestone's comment unless it was enqueued already. True when the milestone is enqueued (now or before) and
    // not finished unsent, so the close may follow it. An entry skipped because the issue was closed is enqueued again.
    private async Task<bool> PostAsync(Pass pass, string milestone, string text, CancellationToken cancellationToken)
    {
        var issue = pass.Issue;
        var status = Status(issue, milestone, pass.Entries);
        if (status is not (EntryStatus.None or EntryStatus.SkippedClosed))
        {
            return status is EntryStatus.Pending or EntryStatus.Sent;
        }

        var checkedText = pass.Guard?.Clean(text, issue.AuthorLogin);
        if (checkedText is { Safe: true })
        {
            var key = NextKey(pass, milestone);
            await outbox.EnqueueAsync(OutboxEntry.Comment(issue.Id, key, checkedText.Text), pass.Transaction, cancellationToken);
            await issues.MarkPostedAsync(issue.Id, milestone, key, pass.Transaction, cancellationToken);
            return true;
        }

        var reason = checkedText is null ? NoBaseUrl : $"guard:{Hash(text)}";
        if (issue.LastPosted.GetValueOrDefault(HeldKey(milestone)) == reason)
        {
            return false;
        }

        var note = checkedText is null
            ? $"Held for a maintainer: the progress update {milestone} was not posted on GitHub. Set the instance base URL (Admin > Instance) so ShipLean can check public text."
            : $"Held for a maintainer: the progress update {milestone} was not posted on GitHub because it failed the public text check ({checkedText.Removed.LastOrDefault() ?? "unsafe"}). It posts once the text passes; until then the issue stays open.";
        await issues.MarkPostedAsync(issue.Id, HeldKey(milestone), reason, pass.Transaction, cancellationToken);
        await using var scope = scopes.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ICommentStore>()
            .AddAsAgentAsync(issue.WorkItemId, new CommentCreate(note), RoundHolds.AgentName, pass.RunAs, pass.Transaction, cancellationToken);
        pass.Held = true;
        return false;
    }

    // The dedupe key for a milestone's next entry: the plain key the first time, then <key>:r1, <key>:r2, ... once that key's entry
    // was skipped or cancelled, so the unique dedupe_key never drops a post that is due again.
    private static string NextKey(Pass pass, string milestone)
    {
        var key = DedupeKey(pass.Issue.Id, milestone);
        var candidate = key;
        var n = 0;
        while (pass.Entries.ContainsKey(candidate))
        {
            n++;
            candidate = $"{key}:r{n.ToString(CultureInfo.InvariantCulture)}";
        }

        pass.Entries[candidate] = new EntryRow(candidate, false, false, null);
        return candidate;
    }

    // Where the entry last_posted records for a milestone stands. None when the milestone has no entry; Sent only when it was sent.
    private static EntryStatus Status(GitHubIssueRecord issue, string milestone, IDictionary<string, EntryRow> entries)
    {
        if (!issue.LastPosted.TryGetValue(milestone, out var key))
        {
            return EntryStatus.None;
        }

        if (!entries.TryGetValue(key, out var entry))
        {
            // Enqueued under a key some other writer already held: the outbox kept that entry, which is not this pass's to judge.
            return EntryStatus.Pending;
        }

        if (entry.Failed)
        {
            return EntryStatus.Finished;
        }

        if (!entry.Sent)
        {
            return EntryStatus.Pending;
        }

        return entry.LastResult switch
        {
            OutboxClaims.SkippedClosed => EntryStatus.SkippedClosed,
            { } result when result.StartsWith("skipped", StringComparison.Ordinal) => EntryStatus.Finished,
            _ => EntryStatus.Sent,
        };
    }

    // The item is no longer done or removed while its close was not sent: the close must not go out. Under the outbox's own
    // per-issue lock (the key enqueue takes), a pending close is finished unsent ('skipped: item reopened'), and so is a pending
    // done or removed comment, then the done, removed and close keys are cleared so a later done posts again, once. False when
    // the close is in flight (leased): nothing changes and a later pass decides.
    private async Task<bool> CancelCloseAsync(GitHubIssueRecord issue, Dictionary<string, EntryRow> entries, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var status = Status(issue, Close, entries);
        if (status is EntryStatus.Sent or EntryStatus.Finished)
        {
            // Already sent or finished: a maintainer decides (D19).
            return true;
        }

        var connection = transaction.Connection!;
        var now = time.GetUtcNow();
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('github_outbox:' || @issueId::text, 0))", new { issueId = issue.Id }, transaction, cancellationToken: cancellationToken));
        if (status == EntryStatus.Pending && await CancelAsync(issue.Id, [issue.LastPosted[Close]], now, transaction, cancellationToken) == 0)
        {
            return false;
        }

        string[] comments = [.. new[] { Done, Removed }.Where(m => Status(issue, m, entries) == EntryStatus.Pending).Select(m => issue.LastPosted[m])];
        await CancelAsync(issue.Id, comments, now, transaction, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE github_issue SET last_posted = last_posted - @keys, updated_at = @now WHERE id = @issueId",
            new { issueId = issue.Id, keys = new[] { Done, DoneSeen, Removed, Close }, now },
            transaction,
            cancellationToken: cancellationToken));
        return true;
    }

    // Finishes the named entries unsent unless they are finished or in flight; returns how many it finished.
    private static async Task<int> CancelAsync(Guid issueId, string[] keys, DateTimeOffset now, DbTransaction transaction, CancellationToken cancellationToken)
    {
        return keys.Length == 0 ? 0 : await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE github_outbox SET sent_at = @now, last_result = @skipped, last_error = NULL
            WHERE github_issue_id = @issueId AND dedupe_key = ANY(@keys) AND sent_at IS NULL AND failed_at IS NULL
              AND (leased_until IS NULL OR leased_until <= @now)
            """,
            new { issueId, keys, now, skipped = SkippedItemReopened },
            transaction,
            cancellationToken: cancellationToken));
    }

    // The accepted proposal's `requires` conditions (C2 JSON); none when there is no stored proposal.
    private static IReadOnlyList<string> Requires(string? proposalJson)
    {
        if (proposalJson is null)
        {
            return [];
        }

        using var document = JsonDocument.Parse(proposalJson);
        return document.RootElement.TryGetProperty("requires", out var requires) && requires.ValueKind == JsonValueKind.Array
            ? [.. requires.EnumerateArray().Where(r => r.ValueKind == JsonValueKind.String).Select(r => r.GetString()!)]
            : [];
    }

    private static string Hash(string text) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..16];

    // The item, its children, its pull requests and, when it is done, the shipped facts, read outside the transaction.
    private async Task<Reading?> ReadAsync(GitHubIssueRecord found, CancellationToken cancellationToken)
    {
        var project = await TakingProject.FindAsync(scopes, found, cancellationToken);
        if (project is null)
        {
            return null;
        }

        var runAs = project.RunAsUserId ?? throw new InvalidOperationException($"Project {project.ProjectKey} takes issues with no run-as member.");
        var item = await ReadItemAsync(found.WorkItemId, cancellationToken);
        if (item is null)
        {
            return null;
        }

        // ShippedInFacts takes MaxKeys keys at a time, and a Feature may have more children than that.
        var facts = new List<ShippedFact>();
        if (item.Category == "completed")
        {
            foreach (var keys in item.Children.Select(c => c.Key).Prepend(item.Key).Chunk(ShippedInFacts.MaxKeys))
            {
                facts.AddRange(await shippedIn.FindAsync(found.ProjectId, keys, cancellationToken));
            }
        }

        return new Reading(runAs, item, [.. facts.OrderBy(f => f.MergedAt).ThenBy(f => f.Key, StringComparer.Ordinal)]);
    }

    private async Task<ItemState?> ReadItemAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var item = await connection.QuerySingleOrDefaultAsync<ItemRow>(new CommandDefinition(
            """
            SELECT w.key, w.title, w.type, s.category, sp.id AS sprintId, sp.name AS sprintName,
                   to_char(sp.start_date, 'YYYY-MM-DD') AS sprintStart, to_char(sp.end_date, 'YYYY-MM-DD') AS sprintEnd
            FROM work_item w
            JOIN state s ON s.id = w.state_id
            LEFT JOIN sprint sp ON sp.id = w.sprint_id AND sp.archived_at IS NULL
            WHERE w.id = @workItemId
            """,
            new { workItemId },
            cancellationToken: cancellationToken));
        if (item is null)
        {
            return null;
        }

        var children = await connection.QueryAsync<ChildRow>(new CommandDefinition(
            "SELECT w.id, w.key, w.title, s.category FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.parent_id = @workItemId ORDER BY w.number",
            new { workItemId },
            cancellationToken: cancellationToken));
        var pulls = await connection.QueryAsync<PullRow>(new CommandDefinition(
            """
            SELECT DISTINCT link_url_key(l.url) AS urlKey, ls.state
            FROM work_item_link l
            JOIN link_state ls ON ls.url_key = link_url_key(l.url)
            WHERE l.source_id = @workItemId AND l.link_type = 'url' AND l.archived_at IS NULL AND link_url_key(l.url) ~ @pattern
            ORDER BY 1
            """,
            new { workItemId, pattern = PullUrlKeyPattern },
            cancellationToken: cancellationToken));
        // A merged pull request of the item or a child whose merge commit isn't reported yet: the done message waits a little for it.
        var mergedWithoutSha = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            """
            SELECT EXISTS (
                SELECT 1 FROM work_item w
                JOIN work_item_link l ON l.source_id = w.id AND l.link_type = 'url' AND l.archived_at IS NULL
                JOIN link_state ls ON ls.url_key = link_url_key(l.url)
                WHERE (w.id = @workItemId OR w.parent_id = @workItemId) AND link_url_key(l.url) ~ @pattern
                  AND ls.state = 'merged' AND (ls.merge_commit_sha IS NULL OR ls.merged_at IS NULL))
            """,
            new { workItemId, pattern = PullUrlKeyPattern },
            cancellationToken: cancellationToken));
        return new ItemState(
            item.Key, item.Title, item.Type, item.Category, item.SprintId, item.SprintName, item.SprintStart, item.SprintEnd, [.. children], [.. pulls], mergedWithoutSha);
    }

    // The item's state category now, in the transaction (null when the item is gone).
    private static async Task<string?> CategoryAsync(Guid workItemId, DbTransaction transaction, CancellationToken cancellationToken)
    {
        return await transaction.Connection!.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT s.category FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.id = @workItemId",
            new { workItemId },
            transaction,
            cancellationToken: cancellationToken));
    }

    // The issue's progress entries in the outbox, by dedupe key.
    private static async Task<Dictionary<string, EntryRow>> EntriesAsync(Guid issueId, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var rows = await transaction.Connection!.QueryAsync<EntryRow>(new CommandDefinition(
            """
            SELECT dedupe_key AS key, sent_at IS NOT NULL AS sent, failed_at IS NOT NULL AS failed, last_result AS lastResult
            FROM github_outbox WHERE github_issue_id = @issueId AND starts_with(dedupe_key, 'progress:')
            """,
            new { issueId },
            transaction,
            cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Key, StringComparer.Ordinal);
    }

    // Accepted issues whose recorded close entry was sent: the pass moves them to closed.
    private async Task<IReadOnlyList<Guid>> ClosesSentAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<Guid>(new CommandDefinition(
            """
            SELECT g.id FROM github_issue g
            JOIN github_outbox o ON o.dedupe_key = g.last_posted ->> 'close' AND o.github_issue_id = g.id
            WHERE g.phase = 'accepted' AND o.sent_at IS NOT NULL AND o.last_result = 'ok'
            ORDER BY g.created_at, g.id
            """,
            cancellationToken: cancellationToken))];
    }

    private static async Task<IReadOnlyCollection<string>> BaseUrlsAsync(DbTransaction transaction, CancellationToken cancellationToken)
        => [.. await transaction.Connection!.QueryAsync<string>(new CommandDefinition(
            "SELECT base_url FROM instance_settings WHERE btrim(base_url) <> ''", transaction: transaction, cancellationToken: cancellationToken))];

    // Every member's display name, archived members included, as the inbox reads them.
    private static async Task<IReadOnlyCollection<string>> MemberNamesAsync(Guid projectId, DbTransaction transaction, CancellationToken cancellationToken)
        => [.. await transaction.Connection!.QueryAsync<string>(new CommandDefinition(
            "SELECT DISTINCT u.name FROM project_member m JOIN app_user u ON u.id = m.user_id WHERE m.project_id = @projectId AND u.name IS NOT NULL",
            new { projectId },
            transaction,
            cancellationToken: cancellationToken))];

    private sealed class Pass(GitHubIssueRecord issue, PublicTextGuard? guard, Guid runAs, DbTransaction transaction, Dictionary<string, EntryRow> entries)
    {
        public IDictionary<string, EntryRow> Entries { get; } = entries;

        public GitHubIssueRecord Issue { get; } = issue;

        public PublicTextGuard? Guard { get; } = guard;

        public Guid RunAs { get; } = runAs;

        public DbTransaction Transaction { get; } = transaction;

        public bool Held { get; set; }
    }

    private sealed record ItemRow(string Key, string Title, string Type, string Category, Guid? SprintId, string? SprintName, string? SprintStart, string? SprintEnd);

    private sealed record ChildRow(Guid Id, string Key, string Title, string Category);

    private sealed record PullRow(string UrlKey, string State);

    private sealed record ItemState(
        string Key, string Title, string Type, string Category, Guid? SprintId, string? SprintName, string? SprintStart, string? SprintEnd,
        IReadOnlyList<ChildRow> Children, IReadOnlyList<PullRow> Pulls, bool MergedWithoutSha);

    private sealed record Reading(Guid RunAs, ItemState Item, IReadOnlyList<ShippedFact> Facts);

    private sealed record EntryRow(string Key, bool Sent, bool Failed, string? LastResult);

    // None: no entry recorded. Pending: enqueued, not finished. Sent: delivered. SkippedClosed: finished unsent because the issue
    // was closed, due again once it is open. Finished: failed, or skipped for any other reason; never enqueued again.
    private enum EntryStatus
    {
        None,
        Pending,
        Sent,
        SkippedClosed,
        Finished,
    }
}
