using ShipLean.Contracts.Prompts;

namespace ShipLean.WebApi.Tests.Prompts;

// What the recording resolver was asked, with the agent it resolved for.
internal sealed class ResolverCalls
{
    private readonly List<(PromptRequest Request, string Agent)> _calls = [];

    public IReadOnlyList<(PromptRequest Request, string Agent)> All
    {
        get
        {
            lock (_calls)
            {
                return [.. _calls];
            }
        }
    }

    public void Add(PromptRequest request, string agent)
    {
        lock (_calls)
        {
            _calls.Add((request, agent));
        }
    }

    public void Clear()
    {
        lock (_calls)
        {
            _calls.Clear();
        }
    }
}
