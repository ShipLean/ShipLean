using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Comments;

// TODO: replaced by the store over code_comment: anchors checked against the item's commits, rendered body, replies, answers, outdating, event.
public sealed class StubCodeComments : ICodeComments
{
    public Task<CodeComment> AddAsync(Guid workItemId, NewCodeComment comment, CancellationToken cancellationToken = default)
        => Task.FromResult(Fake(null, comment.Body));

    public Task<CodeComment> ReplyAsync(Guid workItemId, Guid commentId, string body, CancellationToken cancellationToken = default)
        => Task.FromResult(Fake(commentId, body));

    public Task ResolveAsync(Guid workItemId, Guid commentId, bool resolved, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<IReadOnlyList<CodeComment>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CodeComment>>([Fake(null, "Fake comment")]);

    public Task<IReadOnlyList<CodeComment>> OpenAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CodeComment>>([Fake(null, "Fake comment")]);

    public Task AnswerAsync(Guid workItemId, IReadOnlyList<CodeCommentAnswer> answers, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task MarkOutdatedAsync(Guid workItemId, IReadOnlyList<TaskCommit> added, CancellationToken cancellationToken = default) => Task.CompletedTask;

    private static CodeComment Fake(Guid? parentId, string body)
        => new(Guid.Empty, parentId, "acme/storefront", new string('b', 40), "src/cart.ts", CodeSide.New, 2, null, "const b = 2;", body, $"<p>{body}</p>", "Ada Lovelace", false, DateTimeOffset.UnixEpoch, null, null, null, []);
}
