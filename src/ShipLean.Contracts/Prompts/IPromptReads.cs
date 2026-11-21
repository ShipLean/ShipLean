namespace ShipLean.Contracts.Prompts;

public interface IPromptReads
{
    Task<PromptsPageDto> PageAsync(Guid projectId, ProjectRole role, CancellationToken cancellationToken = default);

    Task<PromptHistoryDto> HistoryAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default);

    Task<PromptAssembledDto> AssembledAsync(Guid projectId, PromptKey key, string agent, CancellationToken cancellationToken = default);
}
