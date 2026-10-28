using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Tests.AgentDispatch;

// An IAgentPrompts that only notes the comment ids it was told about.
internal sealed class RecordingAgentPrompts : IAgentPrompts
{
    private readonly List<Guid> _comments = [];

    public IReadOnlyList<Guid> Comments
    {
        get
        {
            lock (_comments)
            {
                return [.. _comments];
            }
        }
    }

    public Task CommentAddedAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        lock (_comments)
        {
            _comments.Add(commentId);
        }

        return Task.CompletedTask;
    }
}
