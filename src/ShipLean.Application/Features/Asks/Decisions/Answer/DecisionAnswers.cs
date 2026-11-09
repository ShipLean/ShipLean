using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.Decisions.Answer;

// TODO: the decision answers over the decision, ask_event and comment tables replace this stub.
public sealed class DecisionAnswers : IDecisionAnswers
{
    public Task AnswerAsync(Guid workItemId, DecisionAnswer answer, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<IReadOnlyList<int>> ConfirmOpenAsync(Guid workItemId, IReadOnlyList<int> numbers, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task DiscussAsync(Guid workItemId, int number, string body, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
