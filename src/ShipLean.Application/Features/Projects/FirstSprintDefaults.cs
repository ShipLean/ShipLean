using System.Globalization;
using Agoda.IoC.Core;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Features.Projects;

// The New project form's first sprint: starting today, or on the next working day, and lasting two weeks (the end is not clipped).
// A new project names its sprints year, quarter and number ("2026 Q4 Sprint 3"). Dates the form already has (start, and end)
// replace those defaults and the name follows them.
[RegisterPerRequest]
public sealed class FirstSprintDefaults(TimeProvider clock)
{
    public SprintCreate Get(string? workingDays, DateOnly? typedStart = null, DateOnly? typedEnd = null)
    {
        var days = Parse(workingDays);
        var start = typedStart ?? SprintStart.OnWorkingDay(SprintLabels.Today(clock), days);
        var length = typedEnd is { } end ? end.DayNumber - start.DayNumber + 1 : SprintStart.FirstSprintDays;
        if (length < 1)
        {
            throw ApiException.Validation("A sprint can't end before it starts.", "end");
        }

        var name = SprintNaming.Name(SprintNamingConvention.YearQuarterNumber, start, length, _ => false, 1);
        return new SprintCreate(name, start, start.AddDays(length - 1), string.Empty);
    }

    private static List<int> Parse(string? workingDays)
    {
        var days = new List<int>();
        foreach (var part in (workingDays ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var day) || day is < 1 or > 7)
            {
                throw ApiException.Validation("Working days are numbers from 1 (Monday) to 7 (Sunday).", "workingDays");
            }
            days.Add(day);
        }
        return days.Count == 0 ? throw ApiException.Validation("Pick at least one working day.", "workingDays") : days;
    }
}
