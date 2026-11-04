using Dapper;
using Npgsql;
using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.Demos;

// Records published events and, for SprintDemoChanged, reads the sprint's video pointer from another connection at that moment.
internal sealed class RecordedDemoDeliveryEvents(NpgsqlDataSource dataSource) : IProjectEvents
{
    private readonly List<ProjectEvent> _published = [];

    public IReadOnlyList<ProjectEvent> Published => _published;

    public Guid? VideoPointerAtPublish { get; private set; }

    public async Task PublishAsync(ProjectEvent projectEvent)
    {
        _published.Add(projectEvent);
        if (projectEvent is SprintDemoChanged changed)
        {
            await using var connection = await dataSource.OpenConnectionAsync();
            VideoPointerAtPublish = await connection.ExecuteScalarAsync<Guid?>("SELECT demo_video_attachment_id FROM sprint WHERE id = @sprintId", new { changed.SprintId });
        }
    }

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => throw new NotSupportedException();
}
