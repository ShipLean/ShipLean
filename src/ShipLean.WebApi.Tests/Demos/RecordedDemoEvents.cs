using Dapper;
using Npgsql;
using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.Demos;

// An event bus that, when SprintDemoChanged is published, looks from another connection at whether the demo token row is already committed.
internal sealed class RecordedDemoEvents(NpgsqlDataSource dataSource) : IProjectEvents
{
    public bool? TokenVisibleAtPublish { get; private set; }

    public async Task PublishAsync(ProjectEvent projectEvent)
    {
        if (projectEvent is SprintDemoChanged changed)
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            TokenVisibleAtPublish = await connection.ExecuteScalarAsync<bool>(
                "SELECT EXISTS (SELECT 1 FROM sprint s JOIN transfer_token t ON t.work_item_id = s.demo_work_item_id WHERE s.id = @sprintId AND t.kind = 'demo')", new { changed.SprintId });
        }
    }

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => throw new NotSupportedException();
}
