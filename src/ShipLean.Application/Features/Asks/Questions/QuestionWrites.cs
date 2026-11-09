using ShipLean.Contracts.Asks;

namespace ShipLean.Application.Features.Asks.Questions;

// TODO: the question writes over the question, ask_event and comment tables replace this stub.
public sealed class QuestionWrites : IQuestionWrites
{
    public Task<AskedQuestion> AskAsync(Guid workItemId, string text, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task<Guid> AnswerAsync(Guid workItemId, int number, string body, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    public Task WithdrawAsync(Guid workItemId, int number, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
