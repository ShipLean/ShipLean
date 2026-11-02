using ShipLean.Application.Sprints;
using ShipLean.Core;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Retros.Search;

// A sprint of the project with its dates and phase today.
internal sealed record SearchSprint(Guid Id, string Name, DateOnly Start, DateOnly End, SprintPhase Phase)
{
    public SprintRef ToRef() => new(Id, Name, Phase.ToStatus());
}
