namespace ShipLean.Application.Features.InspectionMetrics.Facts;

// Everything the measure and the aggregator read about a project's tasks in a window (K5): the tasks with their story and Feature,
// every state move of them, every Blocked tag change, every review, every commit (with its time when one is stored) and the names
// of the people involved, by user id.
public sealed record LeadTimeFacts(
    IReadOnlyList<LeadTimeTaskFact> Tasks,
    IReadOnlyList<LeadTimeMoveFact> Moves,
    IReadOnlyList<LeadTimeBlockedTagFact> BlockedTags,
    IReadOnlyList<LeadTimeReviewFact> Reviews,
    IReadOnlyList<LeadTimeCommitFact> Commits,
    IReadOnlyDictionary<Guid, string> People)
{
    public static LeadTimeFacts Empty { get; } = new([], [], [], [], [], new Dictionary<Guid, string>());
}
