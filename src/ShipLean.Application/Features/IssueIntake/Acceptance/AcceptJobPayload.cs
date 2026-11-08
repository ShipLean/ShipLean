namespace ShipLean.Application.Features.IssueIntake.Acceptance;

// An accept job's payload: the LGTM comment, the proposal session it accepts, and the Triage item the issue was on when the
// LGTM came (the issue is repointed during the acceptance, so the job carries the Triage item itself).
public sealed record AcceptJobPayload(long CommentId, Guid SessionId, Guid TriageItemId);
