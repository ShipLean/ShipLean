using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Servings;

public sealed class DbPromptServingLog : IPromptServingLog
{
    // TODO: replaced by the real serving log
    public Task RecordAsync(PromptServing serving, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
