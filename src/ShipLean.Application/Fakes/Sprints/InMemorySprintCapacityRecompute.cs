using System.Data.Common;
using ShipLean.Application.Fakes.Holidays;
using ShipLean.Application.Fakes.Projects;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Holidays;
using ShipLean.Contracts.Sprints;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Fakes.Sprints;

// ISprintCapacityRecompute over the planning world, with the same maths as the Postgres one.
public sealed class InMemorySprintCapacityRecompute(InMemoryPlanning world, IPublicHolidays? holidays = null) : ISprintCapacityRecompute
{
    private readonly IPublicHolidays holidays = holidays ?? new InMemoryPublicHolidays();

    public async Task<IReadOnlyList<Guid>> RecomputeAsync(Guid projectId, DbTransaction? transaction, CancellationToken cancellationToken = default)
    {
        List<Guid> open;
        lock (world.Gate)
        {
            world.Project(projectId);
            open = [.. world.Sprints.Where(s => s.ProjectId == projectId && s.Velocity is not null && s.End >= world.Today).OrderBy(s => s.Start).Select(s => s.Id)];
        }

        var changed = new List<Guid>();
        foreach (var id in open)
        {
            var calendar = await InMemorySprintStore.CalendarAsync(world, holidays, id, cancellationToken);
            lock (world.Gate)
            {
                var sprint = world.Sprint(id);
                var entered = sprint.Capacity.Where(c => !c.Value.Archived).Select(c => c.Key).ToList();
                var touched = false;
                foreach (var user in entered)
                {
                    var row = sprint.Capacity[user];
                    var days = row.Days is { } typed ? Math.Min(typed, calendar.WorkingDays) : (decimal?)null;
                    var points = CapacityPoints.For(sprint.Velocity!.Value, entered.Count, calendar.Effective(days), calendar.WorkingDays);
                    if (points != row.Points || days != row.Days)
                    {
                        sprint.Capacity[user] = (points, row.Note, false, days);
                        touched = true;
                    }
                }

                if (touched)
                {
                    changed.Add(id);
                }
            }
        }

        return changed;
    }
}
