using ShipLean.Contracts.Retros;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

// What decide_retro_actions seals: the resolved writes. Applying does exactly this and ignores the other arguments.
public sealed record DecidePlan(
    IReadOnlyList<DecidePlanAction> Actions,
    IReadOnlyList<DecidePlanNotNeeded> NotNeeded,
    IReadOnlyList<DecidePlanTheme> Themes,
    IReadOnlyList<DecidePlanMerge> Merges);
