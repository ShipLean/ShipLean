namespace ShipLean.Contracts.Asks;

// Asks, answers and withdraws questions. Trusts its caller: the caller checks access at Member first. An unknown number
// throws NotFound. Every write publishes AsksChanged after commit, and CommentsChanged when it posted a comment.
public interface IQuestionWrites
{
    Task<AskedQuestion> AskAsync(
        Guid workItemId, string text, IReadOnlyList<string>? options = null, CancellationToken cancellationToken = default);

    // Posts a comment tagged `answer` and marks the question answered, in one transaction. Returns the comment's id.
    // A question that isn't open throws Conflict.
    Task<Guid> AnswerAsync(Guid workItemId, int number, string body, CancellationToken cancellationToken = default);

    // Open only, else Conflict.
    Task WithdrawAsync(Guid workItemId, int number, CancellationToken cancellationToken = default);
}
