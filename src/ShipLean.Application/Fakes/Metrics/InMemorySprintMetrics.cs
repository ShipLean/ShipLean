using System.Text.RegularExpressions;
using ShipLean.Application.Fakes.Projects;
using ShipLean.Contracts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Fakes.Metrics;

public sealed partial class InMemorySprintMetrics(InMemoryPlanning world) : ISprintMetrics
{
    public Task<SprintMetricsDto> GetSprintAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            var sprint = world.Sprint(sprintId);
            var project = world.Project(sprint.ProjectId);
            var committed = world.Committed(sprint);
            var days = Enumerable.Range(0, sprint.End.DayNumber - sprint.Start.DayNumber + 1).Select(sprint.Start.AddDays)
                .Where(d => project.WorkingDays.Contains(d.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)d.DayOfWeek)).ToList();
            var burndown = days.Where(d => d <= world.Today).Select(d => new BurndownPoint(
                d, world.RemainingOn(sprint, d),
                days.Count <= 1 ? 0 : Math.Round(committed * (days.Count - 1 - days.IndexOf(d)) / (days.Count - 1), 2))).ToList();
            var latest = DateOnly.FromDayNumber(Math.Min(world.Today.DayNumber, sprint.End.DayNumber));
            var remaining = latest < sprint.Start ? committed : world.RemainingOn(sprint, latest);
            return Task.FromResult(new SprintMetricsDto(sprintId, committed, world.Completed(sprint), remaining, burndown));
        }
    }

    public Task<VelocityDto> GetVelocityAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            return Task.FromResult(world.Velocity(projectId));
        }
    }

    public Task<ForecastDto> GetForecastAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        lock (world.Gate)
        {
            var velocity = world.Velocity(projectId);
            var used = velocity.Override ?? velocity.Average;
            var buckets = world.Sprints.Where(s => s.ProjectId == projectId && s.Start > world.Today).OrderBy(s => s.Start)
                .Select(s =>
                {
                    var capacity = world.TeamCapacity(s);
                    var full = capacity ?? used;
                    return new Bucket(s.Id, s.Name, full, capacity is not null, full - world.CountedItems(s.Id).Sum(i => i.Points ?? 0));
                }).ToList();
            var all = world.Sprints.Where(s => s.ProjectId == projectId).OrderBy(s => s.Start).ToList();
            var last = all.Count == 0 ? null : all[^1];
            var names = new SprintNameSequence(
                world.Project(projectId).SprintNaming, all.Select(s => s.Name),
                last is null ? world.Today : last.End.AddDays(1), last is null ? SprintStart.FirstSprintDays : last.End.DayNumber - last.Start.DayNumber + 1, NextSprintNumber(projectId));
            var backlog = world.Items.Where(i => i.ProjectId == projectId && i.SprintId is null && i.Counts()
                && world.States.Single(s => s.Id == i.StateId).Category is not (StateCategory.Completed or StateCategory.Removed));
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
            return Task.FromResult(new ForecastDto([.. buckets.Select(b => new ForecastSprintDto(
                b.SprintId, b.Name, Math.Max(0, b.RoomAtStart), b.FromCapacity, b.Taken.LastOrDefault()?.Id, b.Taken.Count,
                b.Taken.Sum(i => i.Points ?? 0), b.Taken.Count(i => i.Points is null)))]));
        }
    }

    // The number after the project's latest sprint name ("Sprint 25" → 26); the sprint count plus one when no name ends in one.
    private int NextSprintNumber(Guid projectId)
    {
        var sprints = world.Sprints.Where(s => s.ProjectId == projectId).OrderBy(s => s.Start).ToList();
        var match = sprints.Count == 0 ? null : TrailingNumber().Match(sprints[^1].Name);
        return match is { Success: true } ? int.Parse(match.Value, System.Globalization.CultureInfo.InvariantCulture) + 1 : sprints.Count + 1;
    }

    [GeneratedRegex(@"\d+$")]
    private static partial Regex TrailingNumber();

    private sealed class Bucket(Guid? sprintId, string name, decimal full, bool fromCapacity, decimal room)
    {
        public Guid? SprintId { get; } = sprintId;

        public string Name { get; } = name;

        public decimal Full { get; } = full;

        public bool FromCapacity { get; } = fromCapacity;

        public decimal RoomAtStart { get; } = room;

        public decimal Room { get; set; } = room;

        public IList<InMemoryPlanning.ItemRow> Taken { get; } = new List<InMemoryPlanning.ItemRow>();
    }
}
