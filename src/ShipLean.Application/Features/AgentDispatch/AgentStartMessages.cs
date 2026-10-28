using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch;

internal static class AgentStartMessages
{
    public static string For(AgentStartBlock block) => block switch
    {
        AgentStartBlock.NoConnection => "This project has no agent connection. Choose one in the project's General settings.",
        AgentStartBlock.NoRepository => "This project has no repository. Add its URL in the project's General settings.",
        _ => "An agent session is already running on this item.",
    };
}
