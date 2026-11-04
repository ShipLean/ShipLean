using Dapper;
using Npgsql;
using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.Demos;

// The host's real bus, noting for each SprintDemoChanged what the sprint's demo columns hold at that moment (read from another connection), so a test can
// tell which write each publish followed: the claim (requested, no message) or a stored message. Read synchronously, so nothing waits on delivery.
internal sealed class RecordedDemoWrites(IProjectEvents inner, NpgsqlDataSource dataSource) : IProjectEvents, IDisposable
{
    private readonly List<(Guid SprintId, bool Requested, string? Message)> _writes = [];

    public IReadOnlyList<(bool Requested, string? Message)> Of(Guid sprintId)
    {
        lock (_writes)
        {
            return [.. _writes.Where(w => w.SprintId == sprintId).Select(w => (w.Requested, w.Message))];
        }
    }

    public async Task PublishAsync(ProjectEvent projectEvent)
    {
        if (projectEvent is SprintDemoChanged changed)
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            var row = await connection.QuerySingleAsync<(bool Requested, string? Message)>(
                "SELECT demo_requested_at IS NOT NULL AS requested, demo_blocked_message AS message FROM sprint WHERE id = @sprintId", new { changed.SprintId });
            lock (_writes)
            {
                _writes.Add((changed.SprintId, row.Requested, row.Message));
            }
        }

        await inner.PublishAsync(projectEvent);
    }

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => inner.Subscribe(projectId, handler);

    public void Dispose() => (inner as IDisposable)?.Dispose();
}
