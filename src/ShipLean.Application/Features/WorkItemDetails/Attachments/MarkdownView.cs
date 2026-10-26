using Agoda.IoC.Core;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// A Markdown attachment for the reader page: the file, its work item, its source, HTML, outline, images and the item's other Markdown files.
// Any member may read it.
[RegisterPerRequest]
public sealed class MarkdownView(IProjectAccess access, IAttachmentStore attachments, IWorkItemStore workItems, MarkdownRenderer renderer)
{
    private const long MaxBytes = 256 * 1024;

    public async Task<AttachmentMarkdownDto> ViewAsync(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Attachment, id, ProjectRole.Member, cancellationToken);
        var (attachment, content) = await attachments.OpenAsync(id, cancellationToken);
        await using (content.Content)
        {
            if (attachment.Kind != AttachmentKind.Markdown)
            {
                throw ApiException.NotMarkdown("That attachment is not a Markdown file.");
            }

            if (attachment.SizeBytes > MaxBytes)
            {
                throw ApiException.MarkdownTooLarge("That Markdown file is over 256 KB, so it is not shown here. Download it instead.");
            }

            using var reader = new StreamReader(content.Content);
            var markdown = await reader.ReadToEndAsync(cancellationToken);
            var item = await workItems.GetAsync(attachment.WorkItemId, cancellationToken);
            var live = await attachments.ListAsync(attachment.WorkItemId, cancellationToken);
            // The 256 KB byte check above is this file's only limit; the 100,000-character cap is for typed Markdown.
            var rendered = renderer.RenderFile(markdown, new MarkdownImageContext(live));
            return new AttachmentMarkdownDto(
                attachment, new MarkdownWorkItemDto(item.Id, item.Key, item.Title, new TypeRef(item.Type, item.Type == WorkItemTypeId.Story ? "User Story" : item.Type.ToString())),
                rendered.Markdown, rendered.Html, MarkdownHeadings.Outline(markdown), rendered.Images,
                [.. live.Where(a => a.Kind == AttachmentKind.Markdown && a.Id != attachment.Id)]);
        }
    }
}
