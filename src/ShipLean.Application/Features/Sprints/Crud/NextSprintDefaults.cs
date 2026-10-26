using System.Globalization;
using System.Text.RegularExpressions;
using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Features.Sprints;

// The New sprint form's starting values: the next number, starting on the first working day after the last sprint (or today
// when there is none), with the last sprint's length. Callers check access first.
[RegisterPerRequest]
public sealed partial class NextSprintDefaults(NpgsqlDataSource dataSource, TimeProvider clock)
{
    public async Task<SprintCreate> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var workingDays = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT array_to_string(working_days, ',') FROM project WHERE id = @projectId", new { projectId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("There is no such project.");
        var working = workingDays.Split(',').Select(day => int.Parse(day, CultureInfo.InvariantCulture)).ToHashSet();
        var sprints = (await connection.QueryAsync<LiveSprint>(new CommandDefinition(
            "SELECT name, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate FROM sprint WHERE project_id = @projectId AND archived_at IS NULL ORDER BY end_date DESC, start_date DESC",
            new { projectId }, cancellationToken: cancellationToken))).ToList();
        var last = sprints.FirstOrDefault();

        var start = SprintStart.OnWorkingDay(last is null ? SprintLabels.Today(clock) : last.End.AddDays(1), working);

        // The same length as the last sprint, in calendar days; only the start moves to a working day.
        var length = last is null ? SprintStart.FirstSprintDays : last.End.DayNumber - last.Start.DayNumber + 1;
        return new SprintCreate($"Sprint {NextNumber(sprints)}", start, start.AddDays(length - 1), string.Empty);
    }

    // One more than the highest number any live sprint's name ends in; the sprint count plus one when none does.
    private static int NextNumber(List<LiveSprint> sprints)
    {
        var numbers = sprints
            .Select(sprint => TrailingNumber().Match(sprint.Name))
            .Where(match => match.Success && int.TryParse(match.Value, NumberStyles.None, CultureInfo.InvariantCulture, out _))
            .Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture))
            .Where(number => number < int.MaxValue)
            .ToList();
        return numbers.Count == 0 ? sprints.Count + 1 : numbers.Max() + 1;
    }

    [GeneratedRegex(@"\d+$")]
    private static partial Regex TrailingNumber();

    private sealed record LiveSprint(string Name, string StartDate, string EndDate)
    {
        public DateOnly Start => DateOnly.ParseExact(StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        public DateOnly End => DateOnly.ParseExact(EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }
}
