namespace ShipLean.Application.Features.Retros.Decide;

// The writes of one run: what a preview resolves and seals, and what an apply does, exactly. Merges are only ever proposed.
public sealed record DecidePlan(
    DecideScope Scope,
    IReadOnlyList<DecidePlanAction> Actions,
    IReadOnlyList<DecidePlanNotNeeded> NotNeeded,
    IReadOnlyList<DecidePlanTheme> Themes,
    IReadOnlyList<DecidePlanMerge> Merges);
