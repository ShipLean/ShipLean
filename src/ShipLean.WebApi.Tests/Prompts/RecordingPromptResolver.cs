using ShipLean.Contracts.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// Returns the built-in text and records each request with the agent a real resolver would have used (the request's, else the connected agent's).
internal sealed class RecordingPromptResolver(IBuiltInPrompts builtIns, IConnectedAgent connectedAgent, ResolverCalls calls) : IPromptResolver
{
    public Task<ResolvedPrompt> ResolveAsync(PromptRequest request, CancellationToken cancellationToken = default)
    {
        var agent = request.Agent ?? connectedAgent.Current.Id;
        calls.Add(request, agent);
        return Task.FromResult(new ResolvedPrompt(request.Key, builtIns.Text(request.Key), null, agent, builtIns.Hash(request.Key)));
    }
}
