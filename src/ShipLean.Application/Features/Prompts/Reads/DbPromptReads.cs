using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Reads;

public sealed class DbPromptReads : IPromptReads
{
    public Task<PromptsPageDto> PageAsync(Guid projectId, ProjectRole role, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(); // TODO: replaced by the real page read

    public Task<PromptHistoryDto> HistoryAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(); // TODO: replaced by the real history read

    public Task<PromptAssembledDto> AssembledAsync(Guid projectId, PromptKey key, string agent, CancellationToken cancellationToken = default)
        => throw new NotImplementedException(); // TODO: replaced by the real assembled read
}
