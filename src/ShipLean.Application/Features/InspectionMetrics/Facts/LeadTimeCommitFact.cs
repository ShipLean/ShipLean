namespace ShipLean.Application.Features.InspectionMetrics.Facts;

// A commit recorded on a task (a commit-titled url link): its 7-character sha and its time (K1), null when none is stored.
public sealed record LeadTimeCommitFact(string ItemKey, string Sha7, DateTimeOffset? CommittedAt);
