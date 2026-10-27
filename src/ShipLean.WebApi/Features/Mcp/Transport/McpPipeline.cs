using ShipLean.Application.Storage;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Http.Features;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Mcp.Scopes;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// The middleware that runs only for /mcp, after authentication and authorization (Program.cs branches here), so the caller
// is a valid agent by now. Other methods than POST pass to the 405 controller. Before the SDK sees a POST it:
//  - buffers the body, up to one max-size attachment in base64 (413), and hands the SDK a fresh copy;
//  - parses the message with the SDK's own reader and refuses anything that is not one valid JSON-RPC message (a batch, a
//    malformed body, a tools/call the SDK could not bind) with 400, never passing it through;
//  - for tools/call, checks the tool's scope against the token's and answers the 403 step-up before the tool runs, and marks
//    a writing call so the per-user write limit counts it.
// An unknown tool name passes through: the SDK answers it. The call-tool filter (ToolScopeFilter) re-checks what the SDK matched. Refusals are ApiError bodies, not JSON-RPC.
public static class McpPipeline
{
    public const string WritingUserItem = "shiplean.mcp.writing-user";

    public static void Configure(IApplicationBuilder branch)
        => branch.Use(async (context, next) =>
        {
            if (!HttpMethods.IsPost(context.Request.Method))
            {
                await next(context);
                return;
            }
            var maxBody = McpBodyLimit.BytesFor(context.RequestServices.GetRequiredService<AttachmentLimits>().MaxBytes);
            // Kestrel's own default is 30 MB, under this cap for a 25 MB file, and it applies when the body is first read.
            var kestrel = context.Features.Get<IHttpMaxRequestBodySizeFeature>();
            if (kestrel is { IsReadOnly: false })
            {
                kestrel.MaxRequestBodySize = maxBody;
            }
            var body = await ReadBodyAsync(context.Request, maxBody);
            if (body is null)
            {
                await Refuse(context.Response, StatusCodes.Status413PayloadTooLarge, context.RequestAborted, ApiException.Validation(McpBodyLimit.TooLarge(maxBody)));
                return;
            }
            context.Request.Body = new MemoryStream(body, writable: false);

            var call = await ReadCallAsync(context.Request, context.RequestAborted);
            if (call is null)
            {
                await Refuse(context.Response, StatusCodes.Status400BadRequest, context.RequestAborted, ApiException.Validation(IsBatch(body)
                    ? "Send one JSON-RPC message per request; batches are not supported."
                    : "Send one valid JSON-RPC message."));
                return;
            }
            if (call.Call is { } toolCall && await RefusedByScopeAsync(context.Request, context.Response, toolCall))
            {
                return;
            }
            context.Request.Body.Position = 0;
            await next(context);
        });

    // The message the SDK will read, parsed the SDK's way (its request reader, type info and options, so a BOM, a charset and
    // property-name case bind identically). Null when it is not a valid JSON-RPC message: never passed through.
    private static async Task<ParsedMessage?> ReadCallAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var message = await request.ReadFromJsonAsync(
                (JsonTypeInfo<JsonRpcMessage>)McpJsonUtilities.DefaultOptions.GetTypeInfo(typeof(JsonRpcMessage)), cancellationToken);
            return message is null ? null : new ParsedMessage(ToolCall.From(message));
        }
        catch (Exception e) when (e is JsonException or InvalidOperationException or NotSupportedException)
        {
            return null;
        }
    }

    private sealed record ParsedMessage(ToolCall? Call);

    private static bool IsBatch(byte[] body)
        => body.AsSpan().TrimStart(Encoding.UTF8.Preamble).TrimStart(" \t\r\n"u8) is [(byte)'[', ..];

    // True when it answered 403.
    private static async Task<bool> RefusedByScopeAsync(HttpRequest request, HttpResponse response, ToolCall call)
    {
        var tools = request.HttpContext.RequestServices.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection;
        if (tools is null || !tools.TryGetPrimitive(call.Tool, out var tool))
        {
            return false;
        }

        var required = ToolScopes.Required(tool, call.Arguments);
        var user = request.HttpContext.User;
        var granted = AgentClaims.ScopesOf(user);
        if (!granted.Contains(required))
        {
            var addresses = await request.HttpContext.RequestServices.GetRequiredService<AgentBaseUrl>().ReadAsync(request, request.HttpContext.RequestAborted);
            response.Headers.WWWAuthenticate = ScopeChallenge.Header(granted, required, addresses.ResourceMetadataUrl);
            await Refuse(response, StatusCodes.Status403Forbidden, request.HttpContext.RequestAborted, ApiException.Forbidden($"This agent was not granted {required}."));
            return true;
        }
        if (ToolScopes.IsWriting(required))
        {
            request.HttpContext.Items[WritingUserItem] = user.FindFirst(ClaimTypes.NameIdentifier)!.Value;
        }
        return false;
    }

    // Null when the body is over the cap.
    private static async Task<byte[]?> ReadBodyAsync(HttpRequest request, long maxBodyBytes)
    {
        if (request.ContentLength > maxBodyBytes)
        {
            return null;
        }
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int read;
        while ((read = await request.Body.ReadAsync(chunk, request.HttpContext.RequestAborted)) > 0)
        {
            await buffer.WriteAsync(chunk.AsMemory(0, read));
            if (buffer.Length > maxBodyBytes)
            {
                return null;
            }
        }
        return buffer.ToArray();
    }

    private static async Task Refuse(HttpResponse response, int status, CancellationToken cancellationToken, ApiException error)
    {
        response.StatusCode = status;
        await response.WriteAsJsonAsync(error.ToApiError(), JsonSerializerOptions.Web, cancellationToken);
    }
}
