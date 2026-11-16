using System.Security.Claims;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.Http;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// What GET /api/downloads/{token} does: the stored file, as the user who asked for the URL, through the same store call as the
// normal download. The token is reusable until it expires. Any token that is not live, and an attachment archived or replaced
// since (the store only opens live rows), and an agent session's run log to a non-admin (D10), answer the same 404. The caller disposes the content.
[RegisterPerRequest]
public sealed class AttachmentDownloads(ITransferTokens tokens, IProjectAccess access, AttachmentStore store, IRunLogAttachments runLogs)
{
    public async Task<(AttachmentDto Attachment, BlobContent Content, bool Inline)> DownloadAsync(string token, HttpRequest request, CancellationToken cancellationToken)
    {
        var grant = await tokens.FindAsync(TransferKind.Download, token, cancellationToken)
            ?? throw ApiException.NotFound("That download link is not valid.");
        var attachmentId = grant.AttachmentId ?? throw ApiException.NotFound("That download link is not valid.");
        // The request acts as the token's user from here on (ICurrentUser reads the principal).
        request.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, grant.UserId.ToString())], "download-token"));
        var (_, role) = await access.RequireAsync(ProjectItem.WorkItem, grant.WorkItemId, ProjectRole.Member, cancellationToken);
        // D10, defence in depth: read_attachment mints no link to a run log for a non-admin, but one minted before a demotion still lives 10 minutes.
        if (await runLogs.IsHiddenAsync(attachmentId, role, cancellationToken))
        {
            throw ApiException.NotFound("That download link is not valid.");
        }

        var served = await store.OpenServedAsync(attachmentId, cancellationToken);
        if (served.Attachment.WorkItemId != grant.WorkItemId)
        {
            await served.Content.Content.DisposeAsync();
            throw ApiException.NotFound("That download link is not valid.");
        }

        return served;
    }
}
