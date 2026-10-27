using ShipLean.Application.Storage;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using ShipLean.Application.Uploads;
using ShipLean.Contracts;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// What the attachment endpoints do: the access check first, then the store. Any member lists, uploads, downloads and archives.
[RegisterPerRequest]
public sealed class AttachmentEndpoints(IProjectAccess access, AttachmentStore store, AttachmentLimits limits)
{
    public async Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        return await store.ListAsync(workItemId, cancellationToken);
    }

    // Access first; only then is the body read, as multipart sections straight into the store (nothing is buffered or spooled).
    // The request is capped at the per-file limit plus the room multipart framing needs, so an oversize body fails at about the limit.
    public async Task<AttachmentDto> UploadAsync(Guid workItemId, HttpRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        var boundary = MultipartUpload.BoundaryOf(request) ?? throw ApiException.Validation("Choose a file to attach.");
        MultipartUpload.CapBody(request, limits.MaxBytes);
        var section = await MultipartUpload.NextFileSectionAsync(new MultipartReader(boundary, request.Body), cancellationToken)
            ?? throw ApiException.Validation("Choose a file to attach.");
        return await store.AddAsync(workItemId, FileNameOf(section), section.ContentType ?? string.Empty, new UnreadableUploadStream(section.Body), cancellationToken: cancellationToken);
    }

    private static string FileNameOf(MultipartSection section)
    {
        var disposition = ContentDispositionHeaderValue.Parse(section.ContentDisposition);
        return (disposition.FileNameStar.HasValue ? disposition.FileNameStar.Value : HeaderUtilities.RemoveQuotes(disposition.FileName).Value) ?? string.Empty;
    }

    // The caller disposes the content.
    public async Task<(AttachmentDto Attachment, BlobContent Content, bool Inline)> DownloadAsync(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Attachment, id, ProjectRole.Member, cancellationToken);
        return await store.OpenServedAsync(id, cancellationToken);
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Attachment, id, ProjectRole.Member, cancellationToken);
        await store.ArchiveAsync(id, cancellationToken);
    }
}
