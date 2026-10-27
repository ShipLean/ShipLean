using ShipLean.Application.Storage;
using System.Security.Claims;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// What PUT /api/uploads/{token} does: the raw body becomes an attachment, as the user who asked for the URL, stamped with the agent and
// attach_file. Any token that is not live answers the same 404. The body is streamed into the store, which enforces the
// per-file limit while it reads; the token is claimed before the body is read and released if the upload fails, so a failed upload can be retried and only one PUT wins.
[RegisterPerRequest]
public sealed class AttachmentUploads(ITransferTokens tokens, IProjectAccess access, IAttachmentStore attachments, AttachmentLimits limits)
{
    private const string Via = "attach_file";

    public async Task<UploadedAttachmentDto> UploadAsync(string token, HttpRequest request, CancellationToken cancellationToken)
    {
        var grant = await tokens.FindAsync(TransferKind.Upload, token, cancellationToken)
            ?? throw ApiException.NotFound("That upload link is not valid.");
        // The request acts as the token's user from here on (ICurrentUser reads the principal).
        request.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, grant.UserId.ToString())], "upload-token"));
        // Claimed before the body is read, so of two concurrent PUTs only one stores. Any failure gives the token back for a retry.
        if (!await tokens.ClaimAsync(token, cancellationToken))
        {
            throw ApiException.NotFound("That upload link is not valid.");
        }

        UploadedAttachmentDto result;
        try
        {
            await access.RequireAsync(ProjectItem.WorkItem, grant.WorkItemId, ProjectRole.Member, cancellationToken);
            if (request.ContentLength > limits.MaxBytes)
            {
                throw ApiException.Validation(limits.FileTooLargeMessage);
            }

            // The store counts the bytes and refuses the one past the limit, so Kestrel's own cap (30 MB by default) is lifted.
            if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } cap)
            {
                cap.MaxRequestBodySize = null;
            }

            var before = await attachments.ListAsync(grant.WorkItemId, cancellationToken);
            var added = await attachments.AddAsync(grant.WorkItemId, grant.Name, grant.ContentType, request.Body, new AttachmentSource(grant.AgentStamp, Via), cancellationToken);
            result = new UploadedAttachmentDto(added.Id, added.Name, added.SizeBytes, before.Any(a => a.Name == added.Name && a.Id != added.Id));
        }
        catch
        {
            await tokens.ReleaseAsync(token, CancellationToken.None);
            throw;
        }

        return result;
    }
}
