using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Servings;

public sealed class DbPromptServingReads : IPromptServingReads
{
    // TODO: replaced by the real serving reads
    public Task<IReadOnlyList<PromptServingDto>> ForRunAsync(
        string projectKey,
        IReadOnlyList<string> itemKeys,
        string? agent,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<PromptServingDto>>([]);
}
