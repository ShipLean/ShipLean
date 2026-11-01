namespace ShipLean.Application.Features.Retros.Sprint;

// A sprint with its dates as yyyy-MM-dd text.
internal sealed record SprintRow(Guid Id, Guid ProjectId, string Name, string Start, string End);
