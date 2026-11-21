namespace ShipLean.Contracts.Prompts;

public interface IPromptServingLog
{
    Task RecordAsync(PromptServing serving, CancellationToken cancellationToken = default);
}
