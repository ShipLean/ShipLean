namespace ShipLean.Application.Features.IssueIntake.Acceptance;

// Thrown inside a create's or a patch's hook so the write rolls back when a concurrent run of the same job claimed that step first.
public sealed class AcceptStepTakenException : Exception
{
}
