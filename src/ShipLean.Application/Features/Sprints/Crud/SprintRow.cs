using System.Globalization;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Sprints;
using ShipLean.Core;

namespace ShipLean.Application.Features.Sprints;

// A sprint row as the shell's list reads it, so a sprint a write returns is the one the list shows afterwards.
// Dates come back as text (to_char) because Dapper maps date columns onto DateOnly only through a handler.
public sealed record SprintRow(Guid Id, Guid ProjectId, string Name, string StartDate, string EndDate, string Goal, string? RolloverStatus)
{
    public const string Columns =
        "id, project_id AS projectId, name, to_char(start_date, 'YYYY-MM-DD') AS startDate, to_char(end_date, 'YYYY-MM-DD') AS endDate, goal, rollover_status AS rolloverStatus";

    public DateOnly Start => DateOnly.ParseExact(StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public DateOnly End => DateOnly.ParseExact(EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    public SprintDto ToDto(DateOnly today) => new(
        Id, Name, Start, End, Goal, SprintLabels.Phase(Start, End, today).ToStatus(),
        RolloverStatus is null ? null : Enum.Parse<RolloverStatus>(RolloverStatus, true),
        SprintLabels.Dates(Start, End, today), SprintLabels.Time(Start, End, today));
}
