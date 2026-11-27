using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Plans;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Migration;

// The startup job (D20): commit link cards on Tasks and Bugs become work_item_commit rows and the card is archived.
// It runs in the background after the app is listening, under a Postgres advisory lock so one instance does it, one GitHub read
// at a time with a pause between them. A card the host answered for finally (repository private or gone, no such commit, not a
// commit URL) stays a link card and is stamped commit_migration_checked_at, so no later run reads it again. A rate limit stops
// the run; a network failure or a 5xx leaves the card unstamped. Either way the next start carries on with what is left.
public sealed class CommitCardMigration(
    NpgsqlDataSource dataSource, IServiceScopeFactory scopes, TimeProvider time, ILogger<CommitCardMigration> logger) : BackgroundService
{
    private const int BatchSize = 100;

    // The pause between two reads of the host. Tests set it to zero.
    public TimeSpan Pause { get; set; } = TimeSpan.FromMilliseconds(250);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Let StartAsync return so the host goes on to listen.
        await Task.Yield();
        try
        {
            await RunAsync(stoppingToken);
        }
        catch (Exception exception) when (!stoppingToken.IsCancellationRequested)
        {
            // An unhandled exception here would stop the host.
            logger.LogError(exception, "Commit card migration failed; it will run again at the next start.");
        }
    }

    // Public so tests can call it. Answers how many cards moved and how many stayed.
    public async Task<(int Moved, int Kept)> RunAsync(CancellationToken cancellationToken)
    {
        await using var lockConnection = await dataSource.OpenConnectionAsync(cancellationToken);
        if (!await lockConnection.ExecuteScalarAsync<bool>(new CommandDefinition(
                CommitCardMigrationLock.TrySql, CommitCardMigrationLock.Parameters, cancellationToken: cancellationToken)))
        {
            logger.LogInformation("Commit card migration skipped: another instance holds the lock.");
            return (0, 0);
        }

        try
        {
            return await MoveAllAsync(cancellationToken);
        }
        finally
        {
            await lockConnection.ExecuteScalarAsync<bool>(new CommandDefinition(
                CommitCardMigrationLock.UnlockSql, CommitCardMigrationLock.Parameters, cancellationToken: CancellationToken.None));
        }
    }

    private async Task<(int Moved, int Kept)> MoveAllAsync(CancellationToken cancellationToken)
    {
        int moved = 0, kept = 0;
        var limited = false;
        var changed = new HashSet<(Guid ProjectId, Guid WorkItemId)>();
        var after = Guid.Empty;
        while (!limited)
        {
            await using var scope = scopes.CreateAsyncScope();
            var rows = scope.ServiceProvider.GetRequiredService<CommitCardRows>();
            var addresses = scope.ServiceProvider.GetRequiredService<IRepoAddresses>();
            var commits = scope.ServiceProvider.GetRequiredService<ITaskCommits>();
            var batch = await rows.NextAsync(after, BatchSize, cancellationToken);
            if (batch.Count == 0)
            {
                break;
            }

            after = batch[^1].Id;
            foreach (var card in batch.Where(c => PlanMarkers.IsCommit(c.Title)))
            {
                if (Pause > TimeSpan.Zero)
                {
                    await Task.Delay(Pause, time, cancellationToken);
                }

                try
                {
                    var parts = await addresses.ParseCommitUrlAsync(card.Url, cancellationToken);
                    await commits.AddAsync(card.WorkItemId, [new CommitRef(parts.Repo.WebUrl, parts.Sha)], CommitSource.Migration, cancellationToken);
                }
                catch (ApiException e) when (e.Code == ApiErrorCodes.RateLimited)
                {
                    logger.LogWarning(e, "Commit card migration stopped: the code host is rate limiting. The rest waits for the next start.");
                    limited = true;
                    break;
                }
                catch (ApiException e) when (e.Code is ApiErrorCodes.RepoUnreadable or ApiErrorCodes.BadSha or ApiErrorCodes.Validation)
                {
                    kept++;
                    if (IsFinal(e))
                    {
                        await rows.MarkCheckedAsync(card.Id, time.GetUtcNow(), cancellationToken);
                    }

                    continue;
                }

                if (await rows.ArchiveAsync(card.Id, time.GetUtcNow(), cancellationToken))
                {
                    moved++;
                    changed.Add((card.ProjectId, card.WorkItemId));
                }
            }
        }

        if (changed.Count > 0)
        {
            await using var scope = scopes.CreateAsyncScope();
            var events = scope.ServiceProvider.GetRequiredService<IProjectEvents>();
            foreach (var (projectId, workItemId) in changed)
            {
                await events.PublishAsync(new LinksChanged(projectId, workItemId));
            }
        }

        logger.LogInformation("Commit card migration: {Moved} moved, {Kept} kept as link cards.", moved, kept);
        return (moved, kept);
    }

    // A read that failed with no HTTP answer (0: network, timeout) or a server error may work later; anything else will not.
    private static bool IsFinal(ApiException e) => e.UpstreamStatus is not (0 or >= 500);
}
