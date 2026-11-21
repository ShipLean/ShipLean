using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Writes;

public sealed class DbPromptWrites : IPromptWrites
{
    public Task<PromptSavedDto> SaveAsync(Guid projectId, PromptKey key, PromptSaveDto save, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(); // TODO: replaced by the real save

    public Task<PromptSavedDto> UseBuiltInAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(); // TODO: replaced by the real use-built-in write

    public Task<PromptSavedDto> UseVersionAsync(Guid projectId, PromptKey key, int number, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(); // TODO: replaced by the real use-version write

    public Task<PromptSavedDto> KeepMineAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(); // TODO: replaced by the real keep-mine write
}
