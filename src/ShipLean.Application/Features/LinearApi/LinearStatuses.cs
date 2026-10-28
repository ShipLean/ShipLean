using ShipLean.Application.Features.AgentDispatch.Rendering;

namespace ShipLean.Application.Features.LinearApi;

// ShipLean's session state as Linear's AgentSessionStatus. Linear has no canceled: a stopped agent ends with a response, which
// Linear reads as complete.
public static class LinearStatuses
{
    public static string Of(string state) => state switch
    {
        "submitted" => AgentSessionStatuses.Pending,
        "working" => AgentSessionStatuses.Active,
        "inputRequired" => AgentSessionStatuses.AwaitingInput,
        "completed" or "canceled" => AgentSessionStatuses.Complete,
        "failed" => AgentSessionStatuses.Error,
        "stale" => AgentSessionStatuses.Stale,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Not an agent session state."),
    };
}
