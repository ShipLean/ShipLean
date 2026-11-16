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

// The one-off startup job (D20): commit link cards on Tasks and Bugs become work_item_commit rows and the card is archived.
// A card whose repository can't be read or whose commit is missing stays a link card. It runs under a Postgres advisory lock
// so one instance does it, and it is idempotent: a second run finds no live commit cards it can move. Like AdminBootstrap it
// works in StartAsync, which blocks startup until it is done.
public sealed class CommitCardMigration(
    NpgsqlDataSource dataSource, IServiceScopeFactory scopes, TimeProvider time, ILogger<CommitCardMigration> logger) : IHostedService
{
    private const int BatchSize = 100;

    public Task StartAsync(CancellationToken cancellationToken) => RunAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

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
        var changed = new HashSet<(Guid ProjectId, Guid WorkItemId)>();
        var after = Guid.Empty;
        while (true)
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
                try
                {
                    var parts = await addresses.ParseCommitUrlAsync(card.Url, cancellationToken);
                    await commits.AddAsync(card.WorkItemId, [new CommitRef(parts.Repo.WebUrl, parts.Sha)], CommitSource.Migration, cancellationToken);
                }
                catch (ApiException e) when (e.Code is ApiErrorCodes.RepoUnreadable or ApiErrorCodes.BadSha or ApiErrorCodes.Validation)
                {
                    kept++;
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
}
