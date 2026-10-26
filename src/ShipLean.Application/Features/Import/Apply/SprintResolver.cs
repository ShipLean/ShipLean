using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import.Jira;
using ShipLean.Contracts.Sprints;

namespace ShipLean.Application.Features.Import.Apply;

// Finds or creates the ShipLean sprint for a Jira sprint, by name within the project. The dates come from SprintSchedule.
public sealed class SprintResolver(NpgsqlDataSource dataSource, ISprintStore sprints, TimeProvider time)
{
    // Sets the dates of every sprint of the project, so an undated one can be created later; call once with all of them before applying issues.
    public void Schedule(ImportRun run, IReadOnlyList<JiraSprint> all)
    {
        var (dates, warnings) = SprintSchedule.Plan(all, DateOnly.FromDateTime(time.GetUtcNow().UtcDateTime));
        foreach (var (id, range) in dates)
        {
            run.SprintDates[id] = range;
        }

        run.SprintWarnings.AddRange(warnings);
    }

    public async Task<Guid> ResolveAsync(ImportRun run, JiraSprint sprint, CancellationToken cancellationToken)
    {
        if (run.Sprints.TryGetValue(sprint.Id, out var known))
        {
            return known;
        }

        var existing = await FindAsync(run.ProjectId, sprint.Name, cancellationToken);
        if (existing is null)
        {
            if (!run.SprintDates.TryGetValue(sprint.Id, out var dates))
            {
                throw ApiException.Validation($"Sprint {sprint.Name} has no dates and was not scheduled.");
            }

            try
            {
                existing = (await sprints.CreateAsync(run.ProjectId, new SprintCreate(sprint.Name.Trim(), dates.Start, dates.End, sprint.Goal ?? string.Empty), null, cancellationToken)).Id;
            }
            catch (ApiException taken) when (taken.Code == ApiErrorCodes.Conflict)
            {
                // A concurrent create made the sprint between the find and the create.
                existing = await FindAsync(run.ProjectId, sprint.Name, cancellationToken);
                if (existing is null)
                {
                    throw;
                }
            }
        }

        run.Sprints[sprint.Id] = existing.Value;
        return existing.Value;
    }

    private async Task<Guid?> FindAsync(Guid projectId, string name, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT id FROM sprint WHERE project_id = @projectId AND lower(name) = lower(@name) AND archived_at IS NULL", new { projectId, name = name.Trim() }, cancellationToken: cancellationToken));
    }
}
