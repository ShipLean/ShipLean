namespace ShipLean.Contracts.Asks;

// The owner's answers to decisions. Trusts its caller: the caller checks access at Member first. An unknown number throws
// NotFound. Every write publishes AsksChanged after commit, and CommentsChanged when it posted a comment.
public interface IDecisionAnswers
{
    // Confirm: open only, else Conflict. Change: open, confirmed or changed (withdrawn is Conflict); from confirmed or changed
    // it also posts a comment tagged `changed` in the same transaction. Withdraw: anything except withdrawn, else Conflict.
    Task AnswerAsync(Guid workItemId, DecisionAnswer answer, CancellationToken cancellationToken = default);

    // Confirms the listed numbers that are still open and skips the rest. Returns the numbers it confirmed.
    Task<IReadOnlyList<int>> ConfirmOpenAsync(Guid workItemId, IReadOnlyList<int> numbers, CancellationToken cancellationToken = default);

    // Posts a comment tagged `about` the decision; the decision is unchanged.
    Task DiscussAsync(Guid workItemId, int number, string body, CancellationToken cancellationToken = default);
}
