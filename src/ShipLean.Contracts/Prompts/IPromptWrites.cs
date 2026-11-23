namespace ShipLean.Contracts.Prompts;

public interface IPromptWrites
{
    // expected: the version the caller read as in use; null skips the check (the web). A different version in use is a conflict.
    Task<PromptSavedDto> SaveAsync(Guid projectId, PromptKey key, PromptSaveDto save, PromptBase? expected = null, CancellationToken cancellationToken = default);

    Task<PromptSavedDto> UseBuiltInAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default);

    Task<PromptSavedDto> UseVersionAsync(Guid projectId, PromptKey key, int number, CancellationToken cancellationToken = default);

    Task<PromptSavedDto> KeepMineAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default);
}
