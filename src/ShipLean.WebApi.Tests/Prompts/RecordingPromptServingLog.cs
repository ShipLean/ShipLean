using ShipLean.Contracts.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// Keeps every serving the resolver records, so a test can read what was logged.
internal sealed class RecordingPromptServingLog : IPromptServingLog
{
    private readonly List<PromptServing> _servings = [];

    public IReadOnlyList<PromptServing> Servings
    {
        get
        {
            lock (_servings)
            {
                return [.. _servings];
            }
        }
    }

    public Task RecordAsync(PromptServing serving, CancellationToken cancellationToken = default)
    {
        lock (_servings)
        {
            _servings.Add(serving);
        }
        return Task.CompletedTask;
    }
}
