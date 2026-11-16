using System.Data.Common;
using System.Security.Claims;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.AgentDispatch.RunLogs;

// POST api/uploads/agent-log?token=: an agent's run log through its session's agentLog transfer link (C10). Any token that is not live, and a
// session creator who is no longer a member, answer the same 404. The body must be text/plain and is counted as it streams (the declared length
// is only an early refusal), so a body that lies about its length is still refused at 1 MiB. The token is claimed before a byte is read and given
// back on any failure before the log is stored, so the link can be retried until it expires; once an upload is stored it is spent, even if
// a later step (the attachmentsChanged publish) throws. The file is stored as the session's creator, stamped with the agent, and set as the
// session's run log in the same transaction. The token is never logged or put in an error.
// The route records no usage itself (an anonymous caller has no actor for the filter), so agent_session.run_log_uploaded is emitted here, once per stored log.
public sealed class AgentRunLogs(
    ITransferTokens tokens, IProjectAccess access, IAttachmentStore attachments, IUsageEvents usage, TimeProvider time, NpgsqlDataSource dataSource) : IAgentRunLogs
{
    private const string Via = AttachmentSource.AgentLogUploadVia;

    public async Task UploadAsync(string token, HttpRequest request, CancellationToken cancellationToken = default)
    {
        var grant = await tokens.FindAsync(TransferKind.AgentLog, token, cancellationToken) ?? throw NotValid();
        var sessionId = grant.SessionId ?? throw NotValid();
        if (!IsPlainText(request.ContentType))
        {
            throw ApiException.Validation("Send the run log as text/plain.");
        }

        if (request.ContentLength > AgentRunLogLinks.MaxBytes)
        {
            throw TooLarge();
        }

        // The request acts as the session's creator from here on (ICurrentUser reads the principal), so the attachment is theirs.
        request.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, grant.UserId.ToString())], "agent-log-token"));
        var projectId = await RequireMemberAsync(grant, cancellationToken);
        if (!await tokens.ClaimAsync(token, cancellationToken))
        {
            throw NotValid();
        }

        StagedAttachment? staged = null;
        try
        {
            staged = await attachments.StageAsync(
                grant.WorkItemId, grant.Name, AgentRunLogLinks.ContentType, request.Body, new AttachmentSource(grant.AgentStamp, Via), AgentRunLogLinks.MaxBytes, cancellationToken);
            var attachmentId = staged.Id;
            await attachments.CommitStagedAsync(grant.WorkItemId, [staged], (transaction, ct) => SetRunLogAsync(transaction, sessionId, grant.WorkItemId, attachmentId, ct), cancellationToken);
        }
        catch
        {
            // CommitStagedAsync publishes after its transaction commits, so a throw can come after the log is stored. Then the link stays spent
            // and the upload is counted; it is given back only when nothing was stored.
            if (staged is not null && await IsRunLogAsync(sessionId, staged.Id))
            {
                TrackUploaded(projectId);
                throw;
            }

            await tokens.ReleaseAsync(token, CancellationToken.None);
            throw;
        }

        TrackUploaded(projectId);
    }

    private void TrackUploaded(Guid projectId)
        => usage.Track(new UsageEvent(
            UsageEventNames.AgentSessionRunLogUploaded, UsageSurface.System, UsageActor.System, projectId, 1, UsageOutcome.Ok, null, null,
            "api:AgentLogUpload.Upload", time.GetUtcNow(), new Dictionary<string, object>()));

    private async Task<bool> IsRunLogAsync(Guid sessionId, Guid attachmentId)
    {
        await using var connection = await dataSource.OpenConnectionAsync(CancellationToken.None);
        return await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM agent_session WHERE id = @sessionId AND run_log_attachment_id = @attachmentId)",
            new { sessionId, attachmentId }, cancellationToken: CancellationToken.None));
    }

    private static ApiException NotValid() => ApiException.NotFound("This upload link isn't valid.");

    private static ApiException TooLarge() => ApiException.Validation($"A run log can be at most {AgentRunLogLinks.MaxBytes / (1024 * 1024)} MB.");

    private static bool IsPlainText(string? contentType)
        => MediaTypeHeaderValue.TryParse(contentType, out var parsed)
            && parsed.MediaType.Equals(AgentRunLogLinks.ContentType, StringComparison.OrdinalIgnoreCase);

    private async Task<Guid> RequireMemberAsync(TransferGrant grant, CancellationToken cancellationToken)
    {
        try
        {
            return (await access.RequireAsync(ProjectItem.WorkItem, grant.WorkItemId, ProjectRole.Member, cancellationToken)).ProjectId;
        }
        catch (ApiException refusal) when (refusal.Code == ApiErrorCodes.NotFound)
        {
            throw NotValid();
        }
    }

    private static async Task SetRunLogAsync(DbTransaction transaction, Guid sessionId, Guid workItemId, Guid attachmentId, CancellationToken cancellationToken)
    {
        var updated = await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            "UPDATE agent_session SET run_log_attachment_id = @attachmentId WHERE id = @sessionId AND work_item_id = @workItemId",
            new { attachmentId, sessionId, workItemId }, transaction, cancellationToken: cancellationToken));
        if (updated != 1)
        {
            throw NotValid();
        }
    }
}
