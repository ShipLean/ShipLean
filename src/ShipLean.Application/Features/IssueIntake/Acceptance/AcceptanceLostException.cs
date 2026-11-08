namespace ShipLean.Application.Features.IssueIntake.Acceptance;

// Thrown under the github_issue row lock when the issue is no longer accepting this job's proposal session, or another worker
// holds the job's lease now: the step rolls back and this run stops.
public sealed class AcceptanceLostException(string message) : Exception(message)
{
}
