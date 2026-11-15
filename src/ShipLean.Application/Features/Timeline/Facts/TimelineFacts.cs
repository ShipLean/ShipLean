namespace ShipLean.Application.Features.Timeline.Facts;

// Everything the timeline builder reads about one Feature (C3), as of one instant. Stories holds its stories and bugs; Tasks their
// tasks. Moves are every state move of the Feature, its stories, bugs and tasks; BlockedTags every Blocked tag change on a task;
// Links only join items of this Feature, normalised to "From must finish before To". Waves maps a story or bug key to its wave
// label from the plan's tags. People maps a user id to the person's name. PlannedBy is who created the Feature's earliest story or
// bug (revision 1), which is who captured the plan or who added that first story; null when there is none.
public sealed record TimelineFacts(
    string ProjectKey,
    TimelineFeatureFact Feature,
    DateTimeOffset? PlanAppliedAt,
    IReadOnlyList<TimelineStoryFact> Stories,
    IReadOnlyList<TimelineTaskFact> Tasks,
    IReadOnlyList<TimelineMoveFact> Moves,
    IReadOnlyList<TimelineBlockedTagFact> BlockedTags,
    IReadOnlyList<TimelineLinkFact> Links,
    IReadOnlyDictionary<string, string> Waves,
    IReadOnlyList<TimelineReviewFact> Reviews,
    IReadOnlyList<TimelineSprintFact> Sprints,
    IReadOnlyDictionary<Guid, string> People,
    TimelineActorFact? PlannedBy = null);
