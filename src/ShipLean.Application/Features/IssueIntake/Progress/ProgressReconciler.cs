using System.Data.Common;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Npgsql;
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
// Feature), then done (the fixed-in message, then Close completed, phase closed) or removed (the message, then Close not planned,
// phase closed). Each post is an outbox entry whose dedupe key is also stored in last_posted under its milestone, in one
// transaction under a per-issue lock, so a replayed or concurrent pass never posts twice. The done message carries
// ShippedInSection over ShippedInFacts (the item's and its children's merged pull requests) and the proposal's requires; while a
// commit's image publication is Pending or Unknown it waits up to 2 h from when the pass first saw the item done (last_posted
// done:seen). Every text passes PublicTextGuard: an unsafe text, or no instance base URL (fail closed), posts nothing and holds the
// milestone for a maintainer with one ShipLean comment per refused text (last_posted held:<milestone>); the phase stays accepted so
// a later pass posts once the text passes, and a done or removed issue is not closed while its message is held.
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

    public static readonly TimeSpan PublishWait = TimeSpan.FromHours(2);

    private const string PullUrlKeyPattern = @"^github\.com/[^/]+/[^/]+/pull/[0-9]+$";

    public static string DedupeKey(Guid issueId, string milestone) => $"progress:{issueId}:{milestone}";

    public static string HeldKey(string milestone) => $"held:{milestone}";

    public static string SprintMilestone(Guid sprintId) => $"sprint:{sprintId}";

    public static string PullMilestone(string urlKey, string state) => $"pr:{urlKey}:{state}";

    public static string ChildMilestone(Guid childId) => $"child:{childId}";

    // One pass over every tracked issue. One issue that fails is logged and the others still run.
    public async Task RunPassAsync(CancellationToken cancellationToken)
    {
        foreach (var issue in await issues.ListAcceptedOpenAsync(null, cancellationToken))
        {
            try
            {
                await ReconcileAsync(issue.Id, cancellationToken);
            }
            catch (Exception failed) when (failed is not OperationCanceledException)
            {
                logger.LogError(failed, "Progress for github_issue {IssueId} ({Issue}) failed", issue.Id, issue.Issue);
            }
        }
    }

    public async Task ReconcileAsync(Guid issueId, CancellationToken cancellationToken)
    {
        var found = await issues.FindByIdAsync(issueId, null, cancellationToken);
        if (found is not { Phase: IssuePhase.Accepted, OpenOnGitHub: true })
        {
            return;
        }

        var project = await TakingProject.FindAsync(scopes, found, cancellationToken);
        if (project is null)
        {
            return;
        }

        var runAs = project.RunAsUserId ?? throw new InvalidOperationException($"Project {project.ProjectKey} takes issues with no run-as member.");
        var item = await ReadItemAsync(found.WorkItemId, cancellationToken);
        if (item is null)
        {
            return;
        }

        // The facts may call GitHub (IImagePublications), so they are read before the transaction and its lock.
        IReadOnlyList<ShippedFact> facts = item.Category == "completed"
            ? await shippedIn.FindAsync(found.ProjectId, [item.Key, .. item.Children.Select(c => c.Key)], cancellationToken)
            : [];

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('issue_progress:' || @issueId::text, 0))", new { issueId }, transaction, cancellationToken: cancellationToken));
        var issue = await issues.FindByIdAsync(issueId, transaction, cancellationToken);
        if (issue is not { Phase: IssuePhase.Accepted, OpenOnGitHub: true } || issue.WorkItemId != found.WorkItemId)
        {
            return;
        }

        var baseUrls = await BaseUrlsAsync(transaction, cancellationToken);
        var guard = baseUrls.Count == 0 ? null : new PublicTextGuard(baseUrls, await MemberNamesAsync(issue.ProjectId, transaction, cancellationToken));
        var pass = new Pass(issue, guard, runAs, transaction);

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
        if (facts.Any(f => f.Publication is ImagePublication.Pending or ImagePublication.Unknown))
        {
            var now = time.GetUtcNow();
            if (!pass.Issue.LastPosted.TryGetValue(DoneSeen, out var seenText))
            {
                await issues.MarkPostedAsync(pass.Issue.Id, DoneSeen, now.ToString("O", CultureInfo.InvariantCulture), pass.Transaction, cancellationToken);
                return;
            }

            var seen = DateTimeOffset.Parse(seenText, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
            if (now - seen < PublishWait)
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

    private async Task CloseAsync(Pass pass, CloseReason reason, CancellationToken cancellationToken)
    {
        await outbox.EnqueueAsync(OutboxEntry.Close(pass.Issue.Id, DedupeKey(pass.Issue.Id, Close), reason), pass.Transaction, cancellationToken);
        if (!await issues.SetPhaseAsync(pass.Issue.Id, [IssuePhase.Accepted], IssuePhase.Closed, pass.Transaction, cancellationToken))
        {
            throw new InvalidOperationException($"github_issue {pass.Issue.Id} left the accepted phase under the progress lock.");
        }
    }

    // Enqueues one milestone's comment unless it was posted already. True when the milestone is posted (now or before).
    private async Task<bool> PostAsync(Pass pass, string milestone, string text, CancellationToken cancellationToken)
    {
        var issue = pass.Issue;
        if (issue.LastPosted.ContainsKey(milestone))
        {
            return true;
        }

        var checkedText = pass.Guard?.Clean(text, issue.AuthorLogin);
        if (checkedText is { Safe: true })
        {
            var key = DedupeKey(issue.Id, milestone);
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
        return new ItemState(item.Key, item.Title, item.Type, item.Category, item.SprintId, item.SprintName, item.SprintStart, item.SprintEnd, [.. children], [.. pulls]);
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

    private sealed class Pass(GitHubIssueRecord issue, PublicTextGuard? guard, Guid runAs, DbTransaction transaction)
    {
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
        IReadOnlyList<ChildRow> Children, IReadOnlyList<PullRow> Pulls);
}
