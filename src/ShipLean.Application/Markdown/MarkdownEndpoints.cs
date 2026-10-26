using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Markdown;

namespace ShipLean.Application.Markdown;

// What POST api/markdown/render does. With a work item the access check comes first (non-members get not_found), and the
// item's attachments are what the images are matched against.
[RegisterPerRequest]
public sealed class MarkdownEndpoints(MarkdownRenderer renderer, IProjectAccess access, IAttachmentStore attachments)
{
    public async Task<RenderedMarkdownDto> RenderAsync(MarkdownRenderRequest request, CancellationToken cancellationToken)
    {
        if (request.WorkItemId is not { } workItemId)
        {
            return renderer.Render(request.Markdown);
        }

        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        return renderer.Render(request.Markdown, new MarkdownImageContext(await attachments.ListAsync(workItemId, cancellationToken)));
    }
}
