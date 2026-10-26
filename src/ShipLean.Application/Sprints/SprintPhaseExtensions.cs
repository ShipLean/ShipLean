using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Sprints;

// Core has no reference to the contracts, so the phase becomes the wire status here.
public static class SprintPhaseExtensions
{
    public static SprintStatus ToStatus(this SprintPhase phase) => phase switch
    {
        SprintPhase.Past => SprintStatus.Past,
        SprintPhase.Current => SprintStatus.Current,
        _ => SprintStatus.Future,
    };
}
