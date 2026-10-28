using Npgsql;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

// One write's transaction: the connection, the moment it stamps on every row, where it came from (for a webhook write,
// which pull or merge request; for an agent, the tool), and the events it publishes once the transaction has committed.
internal sealed class WriteScope(NpgsqlConnection connection, NpgsqlTransaction transaction, DateTimeOffset at, RevisionOrigin origin, string? webhook = null, string? via = null, bool bulk = false)
{
    public NpgsqlConnection Connection { get; } = connection;

    public NpgsqlTransaction Transaction { get; } = transaction;

    public DateTimeOffset At { get; } = at;

    public RevisionOrigin Origin { get; } = origin;

    public string? Webhook { get; } = webhook;

    public bool Bulk { get; } = bulk;

    // The MCP tool name a Feature status stamp records; null from the app.
    public string? Via { get; } = via;

    private readonly List<ProjectEvent> _events = [];

    public IReadOnlyList<ProjectEvent> Events => _events;

    public void Publish(IEnumerable<ProjectEvent> projectEvents) => _events.AddRange(projectEvents);
}
