namespace ShipLean.Contracts.Backlog;

// The ids of the five checks, in the order BacklogReviewDto carries them (the wire strings of review_backlog's flags).
public static class BacklogCheckIds
{
    public const string Unestimated = "unestimated";
    public const string NoAcceptanceCriteria = "no_acceptance_criteria";
    public const string NoParent = "no_parent";
    public const string Stale = "stale";
    public const string LikelyDuplicate = "likely_duplicate";

    public static readonly IReadOnlyList<string> All = [Unestimated, NoAcceptanceCriteria, NoParent, Stale, LikelyDuplicate];
}
