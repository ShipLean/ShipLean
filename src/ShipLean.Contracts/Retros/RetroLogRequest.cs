namespace ShipLean.Contracts.Retros;

// CompiledFrom are the resolved ids of the items the conductor compiled this retro from.
public sealed record RetroLogRequest(
    Guid ProjectId,
    Guid WorkItemId,
    IReadOnlyList<RetroLogPoint> WentWell,
    IReadOnlyList<RetroLogPoint> WentBadly,
    IReadOnlyList<RetroLogPoint> Improve,
    IReadOnlyList<RetroLogPoint> NotInPlan,
    RetroFailureLogDto FailureLog,
    IReadOnlyList<Guid> CompiledFrom,
    bool Partial);
