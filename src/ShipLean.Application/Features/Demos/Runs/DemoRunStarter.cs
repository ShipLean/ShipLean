using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Application.Storage;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Demos.Runs;

// K6. The sprint data and the brand's blob work are read before any transaction and take no lock. The decision (DecideAsync) runs on the winner's
// transaction only: the sprint's requested fields, the revoke of the old links, the new link, the brand's logo link and the prompt. No sprint row
// lock is held across IWorkItemStore, and IWorkItemStore is never called inside a decision.
public sealed class DemoRunStarter(
    NpgsqlDataSource dataSource,
    IWorkItemStore workItems,
    IAgentSessionStarter sessions,
    ISprintDemoData sprintData,
    ISprintDemoPrompt prompt,
    IDemoBrandSource brandSource,
    ITransferTokens transfers,
    IProjectEvents events,
    AttachmentLimits limits,
    TimeProvider time) : IDemoRunStarter
{
    private static readonly TimeSpan LinkLifetime = TimeSpan.FromHours(24);

    public async Task<Guid> GetOrCreateItemAsync(Guid projectId, Guid sprintId, string sprintName, CancellationToken cancellationToken)
    {
        while (true)
        {
            await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
            {
                var existing = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
                    "SELECT demo_work_item_id FROM sprint WHERE id = @sprintId", new { sprintId }, cancellationToken: cancellationToken));
                if (existing is { } id)
                {
                    return id;
                }
            }

            try
            {
                // Webhook origin is the write with no acting person: the history says ShipLean made it, and it works off a request.
                var written = await workItems.CreateAsync(
                    new WorkItemCreate(projectId, WorkItemTypeId.Bau, $"{sprintName} demo", null, null, null),
                    WriteOptions.FromWebhook("ShipLean sprint demo", (transaction, token) => ClaimItemAsync(transaction, projectId, sprintId, token)),
                    cancellationToken);
                return written.Written!.Id;
            }
            catch (DemoItemTakenException)
            {
                // Another caller set the sprint's item first and this create rolled back: read theirs.
            }
        }
    }

    public async Task<AgentStartOutcome> StartSessionAsync(DemoRunRequest request, CancellationToken cancellationToken)
    {
        var data = await sprintData.ReadAsync(request.SprintId, cancellationToken);
        var inspection = await brandSource.InspectAsync(request.ProjectId, cancellationToken);
        var outcome = await sessions.StartWinnerAsync(
            request.ItemId, request.UserId, request.BaseUrl, null,
            async (transaction, token) => (await DecideAsync(transaction, request, data, inspection, token)).Instructions,
            cancellationToken);
        if (outcome.SessionId is not null)
        {
            await events.PublishAsync(new SprintDemoChanged(request.ProjectId, request.SprintId));
        }

        return outcome;
    }

    public async Task<SprintDemoHandover> HandOverAsync(DemoRunRequest request, CancellationToken cancellationToken)
    {
        var data = await sprintData.ReadAsync(request.SprintId, cancellationToken);
        var inspection = await brandSource.InspectAsync(request.ProjectId, cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT id FROM work_item WHERE id = @itemId FOR NO KEY UPDATE", new { itemId = request.ItemId }, transaction, cancellationToken: cancellationToken));
        var live = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            $"SELECT EXISTS (SELECT 1 FROM agent_session WHERE work_item_id = @itemId AND state IN {AgentSessionLiveStates.Sql})",
            new { itemId = request.ItemId }, transaction, cancellationToken: cancellationToken));
        if (live)
        {
            throw ApiException.Conflict("An agent session is already making this demo.");
        }

        var decided = await DecideAsync(transaction, request, data, inspection, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(new SprintDemoChanged(request.ProjectId, request.SprintId));
        return decided;
    }

    // Runs under the project lock as the create's last step: the new item is the project's highest-numbered BAU item.
    private static async Task ClaimItemAsync(DbTransaction transaction, Guid projectId, Guid sprintId, CancellationToken cancellationToken)
    {
        var claimed = await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE sprint SET demo_work_item_id = (SELECT id FROM work_item WHERE project_id = @projectId AND type = 'bau' ORDER BY number DESC LIMIT 1)
            WHERE id = @sprintId AND demo_work_item_id IS NULL
            """,
            new { projectId, sprintId }, transaction, cancellationToken: cancellationToken));
        if (claimed == 0)
        {
            throw new DemoItemTakenException();
        }
    }

    // The winner's writes, all on the transaction. The old links are revoked before the new one is issued; the sprint row is written after the
    // link so it carries the link's own expiry.
    private async Task<SprintDemoHandover> DecideAsync(DbTransaction transaction, DemoRunRequest request, SprintDemoData data, DemoBrandInspection inspection, CancellationToken cancellationToken)
    {
        var connection = transaction.Connection!;
        await transfers.RevokeAsync(TransferKind.Demo, request.ItemId, transaction, cancellationToken);
        await transfers.RevokeAsync(TransferKind.DemoLogo, request.ItemId, transaction, cancellationToken);
        var issued = await transfers.IssueAsync(
            new NewTransfer(TransferKind.Demo, request.ItemId, null, "sprint-demo.mp4", "video/mp4", request.UserId, request.AuthorizationId, request.AgentStamp, LinkLifetime),
            transaction, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE sprint SET demo_requested_by = @userId, demo_requested_via = @via, demo_requested_at = @now, demo_blocked_message = NULL, demo_upload_expires_at = @expires
            WHERE id = @sprintId
            """,
            new { userId = request.UserId, via = request.Via, now = time.GetUtcNow(), expires = issued.ExpiresAt, sprintId = request.SprintId }, transaction, cancellationToken: cancellationToken));
        var brand = await brandSource.PrepareAsync(
            inspection, request.ProjectId, request.ItemId, request.UserId, request.AuthorizationId, request.AgentStamp, request.BaseUrl, transaction, cancellationToken);
        var itemKey = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT key FROM work_item WHERE id = @itemId", new { itemId = request.ItemId }, transaction, cancellationToken: cancellationToken)) ?? string.Empty;
        var baseUrl = request.BaseUrl.TrimEnd('/');
        var uploadUrl = $"{baseUrl}/api/uploads/demo?token={Uri.EscapeDataString(issued.Token)}";
        var target = new DemoUploadTarget(uploadUrl, issued.ExpiresAt, limits.MaxDemoBytes, itemKey, data.Sprint.Name);
        return new SprintDemoHandover(
            request.SprintId,
            itemKey,
            McpOpenUrls.WorkItem(baseUrl, data.Project.Key, itemKey),
            McpOpenUrls.SprintDemo(baseUrl, data.Project.Key, request.SprintId),
            uploadUrl,
            issued.ExpiresAt,
            limits.MaxDemoBytes,
            brand,
            prompt.Render(data, target, brand));
    }
}
