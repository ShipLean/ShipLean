namespace ShipLean.Application.Features.Retros.Store;

// A sprint of a theme series: its dates, and whether it has ended today (only ended sprints count as "after" in a verdict).
public sealed record RetroSeriesSprint(Guid Id, DateOnly Start, DateOnly End, bool Ended);
