using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Asks.Decisions.Answer;

// What the decision routes do: the access check first (any member answers, whoever owns the item), then usage, then the answers.
// Confirm and change are counted here, not in the filter, because they know what the filter cannot (how many, which kind).
// Withdraw and discuss are counted by the filter. MCP answers go to IDecisionAnswers directly, so they count once.
[RegisterPerRequest]
public sealed class DecisionEndpoints(IProjectAccess access, IDecisionAnswers answers, ICurrentUser currentUser, IUsageEvents usage, TimeProvider clock)
{
    public async Task ConfirmAsync(Guid workItemId, ConfirmDecisionsRequest request, CancellationToken cancellationToken)
    {
        var (projectId, _) = await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        var started = clock.GetTimestamp();
        if (request.Numbers is not { Count: > 0 })
        {
            throw ApiException.Validation("Choose at least one decision.", "numbers");
        }

        int count;
        if (request.Numbers.Count == 1)
        {
            await answers.AnswerAsync(
                workItemId, new DecisionAnswer(request.Numbers[0], DecisionAnswerAction.Confirm, null, null, null, null), cancellationToken);
            count = 1;
        }
        else
        {
            count = (await answers.ConfirmOpenAsync(workItemId, request.Numbers, cancellationToken)).Count;
        }

        Track(UsageEventNames.DecisionConfirmed, "WorkItemDecisions.Confirm", projectId, started, count, null);
    }

    public async Task ChangeAsync(Guid workItemId, int number, ChangeDecisionRequest request, CancellationToken cancellationToken)
    {
        var (projectId, _) = await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        var started = clock.GetTimestamp();
        await answers.AnswerAsync(
            workItemId, new DecisionAnswer(number, DecisionAnswerAction.Change, request.Kind, request.AlternativeIndex, request.Text, request.Note), cancellationToken);
        Track(UsageEventNames.DecisionChanged, "WorkItemDecisions.Change", projectId, started, 1, request.Kind);
    }

    public async Task WithdrawAsync(Guid workItemId, int number, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        await answers.AnswerAsync(workItemId, new DecisionAnswer(number, DecisionAnswerAction.Withdraw, null, null, null, null), cancellationToken);
    }

    public async Task DiscussAsync(Guid workItemId, int number, DiscussDecisionRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        await answers.DiscussAsync(workItemId, number, request.Body, cancellationToken);
    }

    private void Track(string name, string key, Guid projectId, long started, int count, AnswerKind? kind)
    {
        var props = new Dictionary<string, object>();
        if (kind is { } k)
        {
            props[UsageProps.AnswerKind] = k.ToString().ToLowerInvariant();
        }

        usage.Track(new UsageEvent(
            name,
            UsageSurface.Web,
            UsageActor.ForUser(currentUser.Id),
            projectId,
            count,
            UsageOutcome.Ok,
            null,
            (long)clock.GetElapsedTime(started).TotalMilliseconds,
            $"api:{key}",
            clock.GetUtcNow(),
            props));
    }
}
