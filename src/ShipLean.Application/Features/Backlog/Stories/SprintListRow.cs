using System.Globalization;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Features.Backlog.Stories;

// Dates come back as text (to_char) because Dapper maps date columns onto DateOnly only through a handler.
public sealed record SprintListRow(Guid Id, string Name, string StartDate, string EndDate)
{
    public SprintRef ToRef(DateOnly today) => new(Id, Name, SprintLabels.Phase(Parse(StartDate), Parse(EndDate), today).ToStatus());

    private static DateOnly Parse(string text) => DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
