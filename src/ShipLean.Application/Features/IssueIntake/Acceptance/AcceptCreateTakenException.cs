namespace ShipLean.Application.Features.IssueIntake.Acceptance;

// Thrown inside the create's hook so the item rolls back when a concurrent run of the same job created it first.
public sealed class AcceptCreateTakenException : Exception
{
}
