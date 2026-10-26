using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.WorkItemDetails.History;

// A work item's field changes, newest first, from its revisions. Any member reads them. An agent's change shows the agent's name.
[RegisterPerRequest]
public sealed class HistoryEndpoints(IProjectAccess access, IWorkItemStore items, NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<HistoryDto> GetAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        var revisions = await items.GetRevisionsAsync(workItemId, cancellationToken);
        var ids = revisions.Select(r => r.ChangedBy).OfType<Guid>().Distinct().ToArray();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var users = (await connection.QueryAsync<UserRef>(new CommandDefinition(
            $"SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = ANY(@ids)", new { ids }, cancellationToken: cancellationToken)))
            .ToDictionary(u => u.Id);
        return new HistoryDto([.. revisions.OrderByDescending(r => r.Rev).Select(r => new RevisionDto(
            r.Rev,
            r.ChangedBy is { } by ? users.GetValueOrDefault(by) : null,
            r.ChangedAt,
            DisplayDate.Relative(r.ChangedAt, clock),
            r.Origin,
            r.Changes,
            r.AgentName,
            r.Webhook))]);
    }
}
