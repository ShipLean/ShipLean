using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Transport;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// Hands an agent the sprint-demo prompt and a one-use upload link. ShipLean never renders the video: the agent does.
[McpServerToolType]
internal sealed class SprintDemoTools(ProjectResolver projects, SprintResolver sprints, ISprintDemos demos, ICurrentAgent agent, CaptureSupport support)
{
    [McpServerTool(Name = "create_sprint_demo"), McpScope(McpScopes.Write)]
    [Description("Make a narrated demo video of a sprint. Returns the instructions to follow as Markdown (read them and do what they say), and a one-use upload link with the curl command that sends the finished video, its chapters and its script. The sprint defaults to the most recently closed one, else the current one. A sprint that is still running is allowed. Fails when the demo already has a live session. The upload link is valid for 24 hours and works once: never write it in a comment, slide or script.")]
    public async Task<CallToolResult> CreateSprintDemo(
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("current, next or a sprint name. Defaults to the most recently closed sprint, else the current one.")] string? sprint = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        var target = sprint is null
            ? await sprints.ResolveDemoDefaultAsync(resolved.Id, cancellationToken)
            : await sprints.ResolveAsync(resolved.Id, sprint, cancellationToken: cancellationToken);

        var baseUrl = await support.BaseUrlAsync(cancellationToken);
        if (RequestOrigin.HostIsClusterService(baseUrl))
        {
            throw ApiException.Validation(
                "There is no upload URL: the only host available is a cluster service name, which a laptop cannot open. Ask an admin to set base_url to the public origin.",
                "sprint");
        }

        var handover = await demos.CreateForAgentAsync(target.Id, baseUrl, AgentStamp.Of(agent) ?? "Agent", support.AgentAuthorizationId(), cancellationToken);
        var brand = handover.Brand;
        var command = $"curl --fail-with-body -F chapters=@chapters.json -F script=@script.md -F video=@demo.mp4 '{handover.UploadUrl}'";
        var result = new CreateSprintDemoResult(
            resolved.Key, McpOpenUrls.SprintDemo(baseUrl, resolved.Key, target.Id), target.Name, handover.ItemKey, handover.ItemUrl,
            handover.UploadUrl, handover.UploadExpiresAt, handover.MaxVideoBytes, command,
            new CreateSprintDemoBrand(brand.LogoIsDefault, brand.LogoUrl, brand.LogoContentType, brand.LogoUrlExpiresAt, brand.TokensAreDefault, brand.LogoFallbackReason));
        var expires = handover.UploadExpiresAt.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);
        return McpToolResult.Of(
            JsonSerializer.SerializeToElement(result), $"{target.Name}: the demo prompt follows. Upload before {expires}; the link works once.", handover.Instructions);
    }
}
