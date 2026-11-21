namespace ShipLean.Contracts.Prompts;

public interface IPromptWrites
{
    Task<PromptSavedDto> SaveAsync(Guid projectId, PromptKey key, PromptSaveDto save, CancellationToken cancellationToken = default);

    Task<PromptSavedDto> UseBuiltInAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default);

    Task<PromptSavedDto> UseVersionAsync(Guid projectId, PromptKey key, int number, CancellationToken cancellationToken = default);

    Task<PromptSavedDto> KeepMineAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default);
}
