using System.Text.Json;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Errors;

// Wraps every tool call. An ApiException a tool throws (a tool never catches it) becomes an error result in the McpError
// shape: IsError, the McpError as the result, and its message as the summary line (McpToolResult). Anything else becomes
// internal_error with the fixed message and is logged; cancellation and protocol errors pass through to the SDK.
public static class ToolErrorFilter
{
    public static McpRequestHandler<CallToolRequestParams, CallToolResult> Apply(McpRequestHandler<CallToolRequestParams, CallToolResult> next)
        => async (request, cancellationToken) =>
        {
            try
            {
                return await next(request, cancellationToken);
            }
            catch (ApiException e)
            {
                return Result(e);
            }
            catch (Exception e) when (e is not OperationCanceledException and not McpProtocolException)
            {
                var internalError = ApiException.Internal();
                request.Services?.GetService<ILoggerFactory>()?.CreateLogger(typeof(ToolErrorFilter))
                    .LogError(e, "Tool {Tool} failed.", request.Params?.Name);
                return Result(internalError);
            }
        };

    public static CallToolResult Result(ApiException exception)
    {
        var error = McpError.Create(exception.Code, exception.Message, exception.Field, exception.Candidates);
        return McpToolResult.Of(JsonSerializer.SerializeToElement(error), error.Error.Message, isError: true);
    }
}
