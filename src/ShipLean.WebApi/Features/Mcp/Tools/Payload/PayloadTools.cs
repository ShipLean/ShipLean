using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Mcp.Tools.Payload;

// Large arguments from a file: the agent asks for an upload URL here, sends the file with curl -T, and calls the tool with
// the payload id. Names no project: the payload is bound to the caller, their agent authorization and the tool.
[McpServerToolType]
internal sealed class PayloadTools(ICurrentAgent agent, CaptureSupport support, IToolPayloads payloads, ICurrentUser currentUser)
{
    [McpServerTool(Name = "create_payload_upload"), McpScope(McpScopes.Write)]
    [Description("Get a one-time upload URL for another tool's large arguments, so the content goes from a file through the shell instead of through you. Use it when the content is already in a file or is over about 20 KB; otherwise send the arguments inline. Without field, the file is a JSON object of that tool's arguments; with field, the file is the raw UTF-8 text of that one string argument, so a Markdown file needs no JSON escaping. The call stores nothing and returns payload, upload_url, expires_at and command (curl -T <file> '<url>'): run the command in the shell within 10 minutes, with the file's path in place of <file>, then call the tool with payload set to the payload id. Arguments you send inline win over the payload's. The payload works only for you, through this connection, for that tool, and you can reuse it for 60 minutes after the upload (a preview and then its apply, for example). It can't carry plan or payload: send those inline. The upload takes up to 2 MB. If base_url is blank and the request arrived on a cluster service name, the call fails: send the arguments inline.")]
    public async Task<CallToolResult> CreatePayloadUpload(
        [Description("The tool the payload is for, such as capture_plan.")] string tool,
        [Description("The one string argument the file holds as raw text, such as content. Leave out when the file is a JSON object of the tool's arguments.")] string? field = null,
        CancellationToken cancellationToken = default)
    {
        // TODO: refuse a tool that takes no payload, and a field that is not one of its text arguments.
        var baseUrl = await support.BaseUrlAsync(cancellationToken);
        if (RequestOrigin.HostIsClusterService(baseUrl))
        {
            throw ApiException.Validation(
                "There is no upload URL: the only host available is a cluster service name, which a laptop cannot open. Send the arguments inline, or ask an admin to set base_url to the public origin.");
        }

        var issued = await payloads.IssueAsync(
            new NewToolPayload(tool, field, currentUser.Id, support.AgentAuthorizationId(), AgentStamp.Of(agent) ?? "Agent"), cancellationToken);
        var url = $"{baseUrl.TrimEnd('/')}/api/uploads/payloads/{issued.UploadToken}";
        var command = $"curl -T <file> '{url}'";
        var result = new PayloadUploadResult(null, baseUrl, tool, field, issued.PayloadId, url, issued.UploadBy, command);
        return CaptureSupport.Reply(result, $"Nothing is stored yet. Run this in the shell within 10 minutes, then call {tool} with payload: \"{issued.PayloadId}\": {command}");
    }
}
