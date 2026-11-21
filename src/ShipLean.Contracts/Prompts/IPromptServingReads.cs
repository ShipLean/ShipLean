namespace ShipLean.Contracts.Prompts;

public interface IPromptServingReads
{
    Task<IReadOnlyList<PromptServingDto>> ForRunAsync(
        string projectKey,
        IReadOnlyList<string> itemKeys,
        string? agent,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default);
}
