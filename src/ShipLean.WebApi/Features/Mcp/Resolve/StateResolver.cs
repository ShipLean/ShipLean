using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Mcp.Resolve;

// Turns a state name into a state of one work item type in the project. States are per type: a name that exists only
// for another type (Task "In review" asked for a Story) is ambiguous, listing the asked type's states so the agent can
// choose; a name no type has is not_found. Archived states never match.
public sealed class StateResolver(NpgsqlDataSource dataSource, IProjectAccess access)
{
    public async Task<ResolvedState> ResolveAsync(Guid projectId, WorkItemTypeId type, string state, string field = "state", CancellationToken cancellationToken = default)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var all = (await connection.QueryAsync<StateRow>(new CommandDefinition(
            "SELECT id, name, type AS typeText, category AS categoryText, (archived_at IS NULL) AS active FROM state WHERE project_id = @projectId",
            new { projectId }, cancellationToken: cancellationToken))).Select(r => r.ToResolved()).ToList();
        var ofType = all.Where(s => s.Active && s.State.Type == type).Select(s => s.State).ToList();
        var archivedOfType = all.Where(s => !s.Active && s.State.Type == type).Select(s => s.State).ToList();

        var wanted = (state ?? string.Empty).Trim();
        if (wanted.Length == 0
            || ofType.Any(s => s.Name.StartsWith(wanted, StringComparison.OrdinalIgnoreCase))
            || archivedOfType.Any(s => NameMatcher.Same(s.Name, wanted)))
        {
            var (match, exact) = NameMatcher.Pick(ofType, archivedOfType, wanted, field, $"{type} state", s => [s.Name], s => [s.Name], s => s.Name);
            return match with { Exact = exact };
        }

        var elsewhere = all.Any(s => s.Active && s.State.Type != type && s.State.Name.StartsWith(wanted, StringComparison.OrdinalIgnoreCase));
        var choices = NameMatcher.Sorted(ofType.Select(s => s.Name));
        return elsewhere
            ? throw ApiException.Ambiguous($"\"{wanted}\" is not a {type} state. A {type} can be: {string.Join(", ", choices)}.", field, choices)
            : throw ApiException.NotFound($"No {type} state matches \"{wanted}\".");
    }

    private sealed record StateRow(Guid Id, string Name, string TypeText, string CategoryText, bool Active)
    {
        public ResolvedStateRow ToResolved() => new(new ResolvedState(Id, Name, Enum.Parse<WorkItemTypeId>(TypeText, true), Enum.Parse<StateCategory>(CategoryText, true)), Active);
    }

    private sealed record ResolvedStateRow(ResolvedState State, bool Active);
}
