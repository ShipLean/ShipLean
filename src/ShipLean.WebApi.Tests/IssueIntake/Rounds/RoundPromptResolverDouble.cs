using ShipLean.Contracts.Prompts;
using ShipLean.WebApi.Tests.Prompts;

namespace ShipLean.WebApi.Tests.IssueIntake.Rounds;

// An IPromptResolver for round tests: serves the text it is given (never the database's), and records each request.
internal sealed class RoundPromptResolverDouble(ResolverCalls calls, RoundPromptText text) : IPromptResolver
{
    public Task<ResolvedPrompt> ResolveAsync(PromptRequest request, CancellationToken cancellationToken = default)
    {
        calls.Add(request, request.Agent ?? PromptAgents.Other);
        return Task.FromResult(new ResolvedPrompt(request.Key, text.Value, null, request.Agent ?? PromptAgents.Other, string.Empty));
    }
}
