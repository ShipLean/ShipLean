namespace ShipLean.Contracts.Prompts;

public interface IPromptResolver
{
    Task<ResolvedPrompt> ResolveAsync(PromptRequest request, CancellationToken cancellationToken = default);
}
