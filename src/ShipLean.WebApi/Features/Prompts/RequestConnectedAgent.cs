using ShipLean.Contracts.Prompts;

namespace ShipLean.WebApi.Features.Prompts;

public sealed class RequestConnectedAgent : IConnectedAgent
{
    // TODO: replaced by the agent captured from the request's initialize, originator and User-Agent
    public ConnectedAgent Current => new(PromptAgents.Other, AgentSignalSource.None, null);
}
