using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Application.Features.Sprints.Metrics;

public sealed partial class SprintMetrics
{
    public async Task<IReadOnlyList<SprintItemOutcomeDto>> GetSprintItemsAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sprint = await connection.QuerySingleOrDefaultAsync<SprintInfo>(new CommandDefinition(MetricsSql.SelectSprint, new { sprintId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("There is no such sprint.");
        var rows = await connection.QueryAsync<OutcomeRow>(new CommandDefinition(
            MetricsSql.Items, new { sprintId, sprint.ProjectId, sprintField = FieldIds.Sprint, stateField = FieldIds.State }, cancellationToken: cancellationToken));
        return [.. rows.Select(row => new SprintItemOutcomeDto(
            row.Id, row.Key, row.Title, Enum.Parse<WorkItemTypeId>(row.Type, ignoreCase: true), row.Points, row.Committed, row.Added, row.Completed, row.Carried))];
    }

    private sealed record OutcomeRow(Guid Id, string Key, string Title, string Type, decimal? Points, bool Committed, bool Added, bool Completed, bool Carried);
}
