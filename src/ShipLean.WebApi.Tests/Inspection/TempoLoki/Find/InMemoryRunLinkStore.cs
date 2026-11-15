using ShipLean.Application.Features.Inspection.TempoLoki;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

// The run-link cache's rows in memory, for tests that have no database.
public sealed class InMemoryRunLinkStore : IRunLinkStore
{
    private readonly Dictionary<(string Project, string Item, string Run), RunLinkRow> stored = [];

    public Task<IReadOnlyList<RunLinkRow>> ReadAsync(string projectKey, string itemKey, DateTimeOffset notBefore, CancellationToken ct)
        => Task.FromResult<IReadOnlyList<RunLinkRow>>([.. stored.Where(r => r.Key.Project == projectKey && r.Key.Item == itemKey && r.Value.FetchedAt > notBefore).Select(r => r.Value)]);

    public Task WriteAsync(string projectKey, string itemKey, IReadOnlyList<RunLinkRow> rows, CancellationToken ct)
    {
        foreach (var row in rows)
        {
            stored[(projectKey, itemKey, row.RunId)] = row;
        }
        return Task.CompletedTask;
    }
}
