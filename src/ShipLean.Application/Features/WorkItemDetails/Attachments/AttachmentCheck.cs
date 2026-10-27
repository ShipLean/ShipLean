using ShipLean.Application.Storage;
using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// What uploading these files would do. Any member may ask. Access first, then one outcome per file in request order.
// A name already on the item is a replace (its bytes are freed); a file over the per-file limit is tooLarge; a file that
// would take the item past its total is overTotal. Files are counted in order, so the same name twice replaces the first.
[RegisterPerRequest]
public sealed class AttachmentCheck(IProjectAccess access, IAttachmentStore store, AttachmentLimits limits)
{
    public async Task<AttachmentCheckDto> CheckAsync(Guid workItemId, AttachmentCheckRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        foreach (var file in request.Files)
        {
            if (string.IsNullOrWhiteSpace(file.Name))
            {
                throw ApiException.Validation("Every file needs a name.", "files");
            }

            if (file.SizeBytes < 0)
            {
                throw ApiException.Validation("A file size cannot be negative.", "files");
            }
        }

        var sizes = (await store.ListAsync(workItemId, cancellationToken))
            .GroupBy(a => a.Name)
            .ToDictionary(g => g.Key, g => g.Sum(a => a.SizeBytes));
        var total = sizes.Values.Sum();
        var outcomes = new List<AttachmentCheckOutcomeDto>();
        foreach (var file in request.Files)
        {
            var name = AttachmentNames.Clean(file.Name);
            if (file.SizeBytes > limits.MaxBytes)
            {
                outcomes.Add(new(file.Name, AttachmentOutcome.TooLarge, limits.FileTooLargeMessage));
                continue;
            }

            var exists = sizes.TryGetValue(name, out var old);
            var next = total - old + file.SizeBytes;
            if (next > limits.MaxItemBytes)
            {
                outcomes.Add(new(file.Name, AttachmentOutcome.OverTotal, limits.ItemFullMessage));
                continue;
            }

            total = next;
            sizes[name] = file.SizeBytes;
            outcomes.Add(new(file.Name, exists ? AttachmentOutcome.Replace : AttachmentOutcome.Add, string.Empty));
        }

        return new AttachmentCheckDto(outcomes);
    }
}
