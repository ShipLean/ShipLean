using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Records;

// github_issue.phase and issue_job.kind as stored: the enum names in camelCase, as they cross the wire.
internal static class IssueWire
{
    public static string Phase(IssuePhase phase) => phase switch
    {
        IssuePhase.Proposing => "proposing",
        IssuePhase.AwaitingReply => "awaitingReply",
        IssuePhase.AlreadyShipped => "alreadyShipped",
        IssuePhase.WaitingForMaintainer => "waitingForMaintainer",
        IssuePhase.Accepted => "accepted",
        IssuePhase.Closed => "closed",
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, null),
    };

    public static IssuePhase ParsePhase(string phase) => phase switch
    {
        "proposing" => IssuePhase.Proposing,
        "awaitingReply" => IssuePhase.AwaitingReply,
        "alreadyShipped" => IssuePhase.AlreadyShipped,
        "waitingForMaintainer" => IssuePhase.WaitingForMaintainer,
        "accepted" => IssuePhase.Accepted,
        "closed" => IssuePhase.Closed,
        _ => throw new InvalidOperationException($"Unknown issue phase '{phase}'."),
    };

    public static string Kind(IssueJobKind kind) => kind switch
    {
        IssueJobKind.Round => "round",
        IssueJobKind.Proposal => "proposal",
        IssueJobKind.Accept => "accept",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
    };

    public static IssueJobKind ParseKind(string kind) => kind switch
    {
        "round" => IssueJobKind.Round,
        "proposal" => IssueJobKind.Proposal,
        "accept" => IssueJobKind.Accept,
        _ => throw new InvalidOperationException($"Unknown issue job kind '{kind}'."),
    };
}
