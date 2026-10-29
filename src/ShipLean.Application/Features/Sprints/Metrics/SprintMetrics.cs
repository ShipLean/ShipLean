using System.Globalization;
using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Features.Sprints.Metrics;

// ISprintMetrics over Postgres. Committed, completed and remaining are aggregated in SQL from the sprint and state changes
// in revision.changes (an item's points are its points now); only the forecast fill and the day list are done here.
public sealed partial class SprintMetrics(NpgsqlDataSource dataSource, TimeProvider clock) : ISprintMetrics
{
    public async Task<SprintMetricsDto> GetSprintAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sprint = await connection.QuerySingleOrDefaultAsync<SprintInfo>(new CommandDefinition(MetricsSql.SelectSprint, new { sprintId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("There is no such sprint.");
        var project = await LoadProjectAsync(connection, sprint.ProjectId, cancellationToken);
        var figures = await FiguresAsync(connection, sprint, cancellationToken);

        var today = SprintLabels.Today(clock);
        var days = Enumerable.Range(0, sprint.End.DayNumber - sprint.Start.DayNumber + 1).Select(sprint.Start.AddDays).ToList();
        var working = days.Where(project.IsWorkingDay).ToList();
        var latest = DateOnly.FromDayNumber(Math.Min(today.DayNumber, sprint.End.DayNumber));
        if (latest < sprint.Start)
        {
            return new SprintMetricsDto(sprintId, figures.Committed, figures.Completed, figures.Committed, []);
        }

        var remaining = (await connection.QueryAsync<RemainingRow>(new CommandDefinition(
            MetricsSql.Remaining, new { sprintId, sprint.ProjectId, last = latest, sprintField = FieldIds.Sprint, stateField = FieldIds.State }, cancellationToken: cancellationToken)))
            .ToDictionary(r => r.Date, r => r.Remaining);
        var burndown = working.Where(d => d <= today).Select(d => new BurndownPoint(
            d, remaining[d], working.Count <= 1 ? 0 : Math.Round(figures.Committed * (working.Count - 1 - working.IndexOf(d)) / (working.Count - 1), 2))).ToList();
        return new SprintMetricsDto(sprintId, figures.Committed, figures.Completed, remaining[latest], burndown);
    }

    public async Task<VelocityDto> GetVelocityAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await VelocityAsync(connection, projectId, cancellationToken);
    }

    public async Task<ForecastDto> GetForecastAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var velocity = await VelocityAsync(connection, projectId, cancellationToken);
        var used = velocity.Override ?? velocity.Average;
        var today = SprintLabels.Today(clock);
        var sprints = (await connection.QueryAsync<ForecastSprintRow>(new CommandDefinition(MetricsSql.ForecastSprints, new { projectId }, cancellationToken: cancellationToken))).ToList();
        var backlog = await connection.QueryAsync<BacklogRow>(new CommandDefinition(MetricsSql.Backlog, new { projectId }, cancellationToken: cancellationToken));

        var buckets = sprints.Where(s => s.Start > today).Select(s =>
        {
            var full = s.Capacity ?? used;
            return new Bucket(s.Id, s.Name, full, s.Capacity is not null, full - s.Planned);
        }).ToList();
        var project = await LoadProjectAsync(connection, projectId, cancellationToken);
        var last = sprints.Count == 0 ? null : sprints[^1];
        var names = new SprintNameSequence(
            SprintNaming.FromColumn(project.Naming), sprints.Select(sprint => sprint.Name),
            last is null ? today : last.End.AddDays(1), last is null ? SprintStart.FirstSprintDays : last.End.DayNumber - last.Start.DayNumber + 1, NextSprintNumber(sprints));
        var at = 0;
        foreach (var item in backlog)
        {
            var points = item.Points ?? 0;
            while (true)
            {
                if (at == buckets.Count)
                {
                    buckets.Add(new Bucket(null, names.Next(), used, false, used));
                }

                var bucket = buckets[at];
                // With no velocity a sprint has no room to fill, so each item gets a sprint to itself.
                if (bucket.Full > 0 ? points <= bucket.Room || bucket.Room == bucket.Full : bucket.Room == bucket.Full && bucket.Taken.Count == 0)
                {
                    bucket.Room -= points;
                    bucket.Taken.Add(item);
                    break;
                }

                at++;
            }
        }

        return new ForecastDto([.. buckets.Select(b => new ForecastSprintDto(
            b.SprintId, b.Name, Math.Max(0, b.RoomAtStart), b.FromCapacity, b.Taken.LastOrDefault()?.Id, b.Taken.Count,
            b.Taken.Sum(i => i.Points ?? 0), b.Taken.Count(i => i.Points is null)))]);
    }

    private async Task<VelocityDto> VelocityAsync(NpgsqlConnection connection, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await LoadProjectAsync(connection, projectId, cancellationToken);
        var ended = await connection.QueryAsync<SprintInfo>(new CommandDefinition(
            MetricsSql.EndedSprints, new { projectId, today = SprintLabels.Today(clock), window = project.VelocityWindow }, cancellationToken: cancellationToken));
        var entries = new List<VelocityEntryDto>();
        foreach (var sprint in ended)
        {
            var figures = await FiguresAsync(connection, sprint, cancellationToken);
            entries.Add(new VelocityEntryDto(sprint.Id, sprint.Name, figures.Committed, figures.Completed));
        }

        var average = entries.Count == 0 ? 0 : Math.Round(entries.Average(e => e.Completed), 2);
        return new VelocityDto(project.VelocityWindow, entries, average, project.VelocityOverride);
    }

    private static Task<Figures> FiguresAsync(NpgsqlConnection connection, SprintInfo sprint, CancellationToken cancellationToken)
        => connection.QuerySingleAsync<Figures>(new CommandDefinition(MetricsSql.Figures, new { sprintId = sprint.Id, sprint.ProjectId, sprintField = FieldIds.Sprint, stateField = FieldIds.State }, cancellationToken: cancellationToken));

    private static async Task<ProjectInfo> LoadProjectAsync(NpgsqlConnection connection, Guid projectId, CancellationToken cancellationToken)
        => await connection.QuerySingleOrDefaultAsync<ProjectInfo>(new CommandDefinition(MetricsSql.SelectProject, new { projectId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("There is no such project.");

    // The number after the project's latest sprint name ("Sprint 25" → 26); the sprint count plus one when no name ends in one.
    private static int NextSprintNumber(List<ForecastSprintRow> sprints)
    {
        var match = sprints.Count == 0 ? null : TrailingNumber().Match(sprints[^1].Name);
        return match is { Success: true } && int.TryParse(match.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number < int.MaxValue
            ? number + 1
            : sprints.Count + 1;
    }

    [GeneratedRegex(@"\d+$")]
    private static partial Regex TrailingNumber();

    private sealed record SprintInfo(Guid Id, Guid ProjectId, string Name, string StartDate, string EndDate)
    {
        public DateOnly Start => DateOnly.ParseExact(StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        public DateOnly End => DateOnly.ParseExact(EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private sealed record ProjectInfo(int VelocityWindow, decimal? VelocityOverride, string WorkingDays, string Naming)
    {
        public bool IsWorkingDay(DateOnly day) => WorkingDays.Split(',').Contains((day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek).ToString(CultureInfo.InvariantCulture));
    }

    private sealed record Figures(decimal Committed, decimal Completed);

    private sealed record RemainingRow(string Day, decimal Remaining)
    {
        public DateOnly Date => DateOnly.ParseExact(Day, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private sealed record ForecastSprintRow(Guid Id, string Name, string StartDate, string EndDate, decimal? Capacity, decimal Planned)
    {
        public DateOnly Start => DateOnly.ParseExact(StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

        public DateOnly End => DateOnly.ParseExact(EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
    }

    private sealed record BacklogRow(Guid Id, decimal? Points);

    private sealed class Bucket(Guid? sprintId, string name, decimal full, bool fromCapacity, decimal room)
    {
        public Guid? SprintId { get; } = sprintId;

        public string Name { get; } = name;

        public decimal Full { get; } = full;

        public bool FromCapacity { get; } = fromCapacity;

        public decimal RoomAtStart { get; } = room;

        public decimal Room { get; set; } = room;

        public IList<BacklogRow> Taken { get; } = new List<BacklogRow>();
    }
}
