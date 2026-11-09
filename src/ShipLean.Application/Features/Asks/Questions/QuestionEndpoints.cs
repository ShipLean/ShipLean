using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Asks.Questions;

// What the question routes do: the access check first (any member answers or withdraws, not only the owner), then the write.
[RegisterPerRequest]
public sealed class QuestionEndpoints(IProjectAccess access, IQuestionWrites questions)
{
    public async Task AnswerAsync(Guid workItemId, int number, AnswerQuestionRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        if (string.IsNullOrWhiteSpace(request?.Body))
        {
            throw ApiException.Validation("Write an answer.", "body");
        }

        await questions.AnswerAsync(workItemId, number, request.Body, cancellationToken);
    }

    public async Task WithdrawAsync(Guid workItemId, int number, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        await questions.WithdrawAsync(workItemId, number, cancellationToken);
    }
}
