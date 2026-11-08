using Dapper;
using Npgsql;
using ShipLean.Application.Events;
using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.IssueIntake.InboxWorker;

// The real in-memory bus, which also records each CommentsChanged with how many ShipLean comments its item had in committed data
// at the moment it was published, read on a separate connection: zero means it was published before its transaction committed.
internal sealed class CommitCheckingProjectEvents(InMemoryProjectEvents inner, NpgsqlDataSource dataSource) : IProjectEvents
{
    private readonly List<(Guid WorkItemId, int Committed)> _published = [];

    public IReadOnlyList<(Guid WorkItemId, int Committed)> Published
    {
        get
        {
            lock (_published)
            {
                return [.. _published];
            }
        }
    }

    public async Task PublishAsync(ProjectEvent projectEvent)
    {
        if (projectEvent is CommentsChanged changed)
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            var committed = await connection.ExecuteScalarAsync<int>(
                "SELECT count(*)::int FROM comment WHERE work_item_id = @WorkItemId AND agent_name = 'ShipLean'", new { changed.WorkItemId });
            lock (_published)
            {
                _published.Add((changed.WorkItemId, committed));
            }
        }

        await inner.PublishAsync(projectEvent);
    }

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => inner.Subscribe(projectId, handler);
}
