using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

// Asks for a proposal round (C4, D11, D16), all in the caller's transaction, under the project's advisory lock so the rate count,
// the round count and the enqueue are one decision per project at a time:
// - a round job of the issue that is still pending already covers this request (its session reads the comments when it starts),
//   so a second request, concurrent or not, enqueues nothing more;
// - unless forced (a maintainer's /shiplean), at most RatePerHour round jobs per project in the last hour and rounds below the
//   project's maximum; past either the issue is held (RoundHolds);
// - otherwise the rounds go up by one, the job is enqueued with key round:<n>:<issue>, and the phase becomes proposing.
// An issue whose repository its project no longer takes is held with nothing posted (intakeOff).
public sealed class IssueProposalSessions(
    IGitHubIssueStore issues, IIssueJobs jobs, RoundHolds holds, IServiceScopeFactory scopes, TimeProvider clock) : IIssueProposalSessions
{
    public const int RatePerHour = 20;

    public const string IntakeOff = "intakeOff";

    private static readonly IssuePhase[] Restartable = [IssuePhase.AwaitingReply, IssuePhase.AlreadyShipped, IssuePhase.WaitingForMaintainer];

    public static string JobKey(int round, Guid issueId) => $"round:{round}:{issueId}";

    public async Task<RoundRequest> RequestRoundAsync(Guid issueId, bool force, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection!;
        var projectId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT project_id FROM github_issue WHERE id = @issueId", new { issueId }, transaction, cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"No github_issue {issueId}.");
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('issue_round:' || @projectId::text, 0))", new { projectId }, transaction, cancellationToken: cancellationToken));
        var issue = await issues.FindByIdAsync(issueId, transaction, cancellationToken)
            ?? throw new InvalidOperationException($"No github_issue {issueId}.");

        var project = await TakingProject.FindAsync(scopes, issue, cancellationToken);
        if (project is null)
        {
            return RoundRequest.Held(IntakeOff);
        }

        if (await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM issue_job WHERE github_issue_id = @issueId AND kind = 'round' AND status = 'pending')",
            new { issueId }, transaction, cancellationToken: cancellationToken)))
        {
            return RoundRequest.Requested;
        }

        if (!force)
        {
            if (await jobs.CountSinceAsync(projectId, IssueJobKind.Round, clock.GetUtcNow() - TimeSpan.FromHours(1), transaction, cancellationToken) >= RatePerHour)
            {
                return await holds.HoldAsync(issue, RoundHolds.RateLimit, transaction, cancellationToken);
            }

            if (issue.Rounds >= project.MaxRounds)
            {
                return await holds.HoldAsync(issue, RoundHolds.RoundCap, transaction, cancellationToken);
            }
        }

        var round = await issues.IncrementRoundsAsync(issueId, transaction, cancellationToken);
        await jobs.EnqueueAsync(IssueJobKind.Round, JobKey(round, issueId), issueId, projectId, JsonSerializer.Serialize(new RoundJobPayload(round)), transaction, cancellationToken);
        await issues.SetPhaseAsync(issueId, Restartable, IssuePhase.Proposing, transaction, cancellationToken);
        return RoundRequest.Requested;
    }
}
