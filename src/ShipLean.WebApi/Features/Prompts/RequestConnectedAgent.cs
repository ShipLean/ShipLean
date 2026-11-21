using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;

namespace ShipLean.WebApi.Features.Prompts;

// The agent connected on this request: the signals the bearer scheme stored in the request's items, recognised by PromptAgents.Of.
// Off a request, or on one without signals, it is (other, None, null).
public sealed class RequestConnectedAgent(ICurrentRequest request) : IConnectedAgent
{
    public const string SignalsItemKey = "shiplean.agent-signals";

    public ConnectedAgent Current
        => request.Exists && request.Items?.TryGetValue(SignalsItemKey, out var value) == true && value is AgentSignals signals
            ? PromptAgents.Of(signals)
            : new ConnectedAgent(PromptAgents.Other, AgentSignalSource.None, null);
}
