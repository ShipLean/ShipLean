using System.Data.Common;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.IssueIntake.Records;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

// Asks for a proposal round (C4, D11, D16), all in the caller's transaction, under the project's advisory lock so the rate count,
// the round count and the enqueue are one decision per project at a time:
// - a round job of the issue that is still pending already covers this request (its session reads the comments when it starts),
//   so a second request, concurrent or not, enqueues nothing more; when that job's session has already started (its
//   round-session:<jobKey> effect is claimed), the comments were read before this reply, so rerun_requested is set for the inbox;
// - unless forced (a maintainer's /shiplean), at most RatePerHour round jobs per project in the last hour and rounds below the
//   project's maximum; past either the issue is held, with its notices (RoundHolds);
// - otherwise the rounds go up by one, the job is enqueued with key round:<n>:<issue>, and the phase becomes proposing.
// An issue whose repository its project no longer takes is held with nothing written (intakeOff). While an LGTM's accept job
// runs (phase accepting) nothing is written either (accepting): the row lock here waits for the LGTM's commit, or wins first.
public sealed class IssueProposalSessions(
    IGitHubIssueStore issues, IIssueJobs jobs, IIssueEffects effects, RoundHolds holds, IServiceScopeFactory scopes, TimeProvider clock) : IIssueProposalSessions
{
    public const int RatePerHour = 20;

    public const string IntakeOff = "intakeOff";

    public const string Accepting = "accepting";

    public const string Continue = "A maintainer can comment /shiplean on the GitHub issue to continue.";

    private static readonly IssuePhase[] Restartable = [IssuePhase.AwaitingReply, IssuePhase.AlreadyShipped, IssuePhase.WaitingForMaintainer];

    public static string JobKey(int round, Guid issueId) => $"round:{round}:{issueId}";

    // Claimed with the session id inside the session start's transaction (RoundJobWorker), so a job starts one session at most.
    public static string SessionKey(string jobKey) => $"round-session:{jobKey}";

    public async Task<RoundRequest> RequestRoundAsync(Guid issueId, bool force, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection!;
        var projectId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT project_id FROM github_issue WHERE id = @issueId", new { issueId }, transaction, cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"No github_issue {issueId}.");
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('issue_round:' || @projectId::text, 0))", new { projectId }, transaction, cancellationToken: cancellationToken));
        var phase = await connection.QuerySingleAsync<string>(new CommandDefinition(
            "SELECT phase FROM github_issue WHERE id = @issueId FOR UPDATE", new { issueId }, transaction, cancellationToken: cancellationToken));
        if (IssueWire.ParsePhase(phase) == IssuePhase.Accepting)
        {
            return RoundRequest.Held(Accepting);
        }

        var issue = await issues.FindByIdAsync(issueId, transaction, cancellationToken)
            ?? throw new InvalidOperationException($"No github_issue {issueId}.");

        var project = await TakingProject.FindAsync(scopes, issue, cancellationToken);
        if (project is null)
        {
            return RoundRequest.Held(IntakeOff);
        }

        var pending = await connection.QueryFirstOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT key FROM issue_job WHERE github_issue_id = @issueId AND kind = 'round' AND status = 'pending' ORDER BY created_at DESC LIMIT 1",
            new { issueId }, transaction, cancellationToken: cancellationToken));
        if (pending is not null)
        {
            if (await effects.FindAsync(SessionKey(pending), transaction, cancellationToken) is not null)
            {
                await issues.RequestRerunAsync(issueId, transaction, cancellationToken);
            }

            return RoundRequest.Requested;
        }

        if (!force)
        {
            if (await jobs.CountSinceAsync(projectId, IssueJobKind.Round, clock.GetUtcNow() - TimeSpan.FromHours(1), transaction, cancellationToken) >= RatePerHour)
            {
                return await HoldAsync(
                    issue, RoundHolds.RateLimit, $"Held for a maintainer: this project already started {RatePerHour} proposal rounds in the last hour, its limit.", project, transaction, cancellationToken);
            }

            if (issue.Rounds >= project.MaxRounds)
            {
                return await HoldAsync(
                    issue, RoundHolds.RoundCap, $"Held for a maintainer: this issue had {issue.Rounds} proposal rounds, the project's maximum of {project.MaxRounds}.", project, transaction, cancellationToken);
            }
        }

        var round = await issues.IncrementRoundsAsync(issueId, transaction, cancellationToken);
        await jobs.EnqueueAsync(IssueJobKind.Round, JobKey(round, issueId), issueId, projectId, JsonSerializer.Serialize(new RoundJobPayload(round)), transaction, cancellationToken);
        await issues.SetPhaseAsync(issueId, Restartable, IssuePhase.Proposing, transaction, cancellationToken);
        return RoundRequest.Requested;
    }

    // The hold notices are written here, so every caller (intake, replies, /shiplean) gets them. The caller owns the transaction,
    // so it publishes RoundRequest.Changed after it commits.
    private async Task<RoundRequest> HoldAsync(
        GitHubIssueRecord issue, string reason, string explanation, IssueIntakeProject project, DbTransaction transaction, CancellationToken cancellationToken)
    {
        var runAs = project.RunAsUserId ?? throw new InvalidOperationException($"Project {project.ProjectKey} takes issues with no run-as member.");
        var noted = await holds.HoldAsync(issue, reason, $"{explanation} {Continue}", runAs, transaction, cancellationToken);
        return RoundRequest.Held(reason, noted ? new CommentsChanged(issue.ProjectId, issue.WorkItemId) : null);
    }
}
