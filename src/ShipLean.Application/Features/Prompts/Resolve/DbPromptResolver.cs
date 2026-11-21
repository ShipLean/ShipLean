using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Resolve;

public sealed class DbPromptResolver : IPromptResolver
{
    public Task<ResolvedPrompt> ResolveAsync(PromptRequest request, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(); // TODO: replaced by the real resolver
}
