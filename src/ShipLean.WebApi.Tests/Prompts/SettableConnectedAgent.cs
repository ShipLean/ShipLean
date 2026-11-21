using ShipLean.Contracts.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// A connected agent a test sets, standing in for the recognition from the request.
internal sealed class SettableConnectedAgent : IConnectedAgent
{
    public volatile string Id = PromptAgents.Other;

    public ConnectedAgent Current => new(Id, AgentSignalSource.None, null);
}
