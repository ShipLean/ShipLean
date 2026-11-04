using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Brand;

// What GET /api/downloads/demo-logo does, as AttachmentDownloads does for a file: the token's user must still be a member, and the logo is read fresh
// from the project's row, so one removed since the link was made is gone at once. Every token that is not live, a lost membership and a missing logo
// answer the same 404. The token is not spent. The caller disposes the content.
public sealed class DemoLogoDownload(ITransferTokens tokens, IProjectAccess access, IDemoLogoStore store) : IDemoLogoDownload
{
    private const string NotValid = "That download link is not valid.";

    public async Task<BlobContent> OpenAsync(string token, HttpRequest request, CancellationToken cancellationToken = default)
    {
        var grant = await tokens.FindAsync(TransferKind.DemoLogo, token, cancellationToken) ?? throw ApiException.NotFound(NotValid);
        // The request acts as the token's user from here on (ICurrentUser reads the principal).
        request.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, grant.UserId.ToString())], "download-token"));
        Guid projectId;
        try
        {
            (projectId, _) = await access.RequireAsync(ProjectItem.WorkItem, grant.WorkItemId, ProjectRole.Member, cancellationToken);
        }
        catch (ApiException refused) when (refused.Code == ApiErrorCodes.NotFound)
        {
            throw ApiException.NotFound(NotValid);
        }

        return await store.TryOpenAsync(projectId, cancellationToken) ?? throw ApiException.NotFound(NotValid);
    }
}
