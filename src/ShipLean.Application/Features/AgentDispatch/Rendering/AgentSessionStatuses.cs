namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// Linear's AgentSessionStatus values, as they appear in `agentSession.status`.
public static class AgentSessionStatuses
{
    public const string Pending = "pending";
    public const string Active = "active";
    public const string AwaitingInput = "awaitingInput";
    public const string Complete = "complete";
    public const string Error = "error";
    public const string Stale = "stale";

    private static readonly string[] All = [Pending, Active, AwaitingInput, Complete, Error, Stale];

    public static string Require(string status)
        => All.Contains(status, StringComparer.Ordinal) ? status : throw new ArgumentOutOfRangeException(nameof(status), status, "Not an AgentSessionStatus.");
}
