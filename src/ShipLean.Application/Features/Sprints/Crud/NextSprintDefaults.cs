using System.Globalization;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Features.Sprints;

// The New sprint form's starting values: the project's next default name, starting on the first working day after the last sprint
// (or today when there is none), with the last sprint's length. Dates the caller already typed (start, and end) replace those
// defaults, and the name and length follow them. Callers check access first.
[RegisterPerRequest]
public sealed class NextSprintDefaults(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<SprintCreate> GetAsync(Guid projectId, DateOnly? typedStart, DateOnly? typedEnd, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var project = await connection.QuerySingleOrDefaultAsync<ProjectRow>(new CommandDefinition(
            "SELECT array_to_string(working_days, ',') AS workingDays, sprint_naming AS naming FROM project WHERE id = @projectId", new { projectId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("There is no such project.");
        var working = project.WorkingDays.Split(',').Select(day => int.Parse(day, CultureInfo.InvariantCulture)).ToHashSet();
        var sprints = (await connection.QueryAsync<LiveSprint>(new CommandDefinition(
            "SELECT name, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate FROM sprint WHERE project_id = @projectId AND archived_at IS NULL ORDER BY end_date DESC, start_date DESC",
            new { projectId }, cancellationToken: cancellationToken))).ToList();
        var last = sprints.FirstOrDefault();

        var start = typedStart ?? SprintStart.OnWorkingDay(last is null ? SprintLabels.Today(clock) : last.End.AddDays(1), working);

        // The same length as the last sprint, in calendar days, unless the caller typed an end; only the start moves to a working day.
        var lastLength = last is null ? SprintStart.FirstSprintDays : last.End.DayNumber - last.Start.DayNumber + 1;
        var length = typedEnd is { } typed ? typed.DayNumber - start.DayNumber + 1 : lastLength;
        if (length < 1)
        {
            throw ApiException.Validation("A sprint can't end before it starts.", "end");
        }

        var names = sprints.Select(sprint => sprint.Name).ToList();
        var name = SprintNaming.Name(SprintNaming.FromColumn(project.Naming), start, length, taken: names.Contains, SprintNaming.NextNumber(names));
        return new SprintCreate(name, start, start.AddDays(length - 1), string.Empty);
    }

    private sealed record ProjectRow(string WorkingDays, string Naming);

    private sealed record LiveSprint(string Name, string StartDate, string EndDate)
    {
        public DateOnly Start => DateOnly.ParseExact(StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        public DateOnly End => DateOnly.ParseExact(EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
