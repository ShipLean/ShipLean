using System.Globalization;
using Agoda.IoC.Core;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Features.Projects;

// The New project form's Sprint 1: starting today, or on the next working day, and lasting two weeks (the end is not clipped).
[RegisterPerRequest]
public sealed class FirstSprintDefaults(TimeProvider clock)
{
    public SprintCreate Get(string? workingDays)
    {
        var days = Parse(workingDays);
        var start = SprintStart.OnWorkingDay(SprintLabels.Today(clock), days);
        return new SprintCreate("Sprint 1", start, start.AddDays(SprintStart.FirstSprintDays - 1), string.Empty);
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
