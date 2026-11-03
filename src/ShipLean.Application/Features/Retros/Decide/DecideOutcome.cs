namespace ShipLean.Application.Features.Retros.Decide;

// A preview (nothing written) or an apply. WouldTheme and WouldMerges are what was themed and proposed once applied. Created lists the keys an apply made.
public sealed record DecideOutcome(
    IReadOnlyList<DecideCreateLine> WouldCreate,
    IReadOnlyList<DecideAddLine> WouldAdd,
    IReadOnlyList<DecideNotNeededLine> WouldMarkNotNeeded,
    int WouldTheme,
    int WouldProposeMerges,
    IReadOnlyList<DecideSkip> Skipped,
    int LeftUndecided,
    IReadOnlyList<string> Created,
    DecidePlan Plan);
