using System.Text.Json;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;

namespace ShipLean.WebApi.Features.Mcp.Errors;

// Wraps every prompts/get. A prompt cannot return an error result, and the SDK turns any exception that is not an
// McpException into -32603 "An error occurred.", losing the code. So an ApiException a prompt throws (a prompt never
// catches it) becomes an McpProtocolException: -32602 (-32603 for internal_error), its message, and the McpError body as
// error.data, as JsonElements, which the SDK passes through. Anything else is left to the SDK.
public static class PromptErrorFilter
{
    public static McpRequestHandler<GetPromptRequestParams, GetPromptResult> Apply(McpRequestHandler<GetPromptRequestParams, GetPromptResult> next)
        => async (request, cancellationToken) =>
        {
            try
            {
                return await next(request, cancellationToken);
            }
            catch (ApiException e)
            {
                var error = McpError.Create(e.Code, e.Message, e.Field, e.Candidates);
                var code = e.Code == ApiErrorCodes.Internal ? McpErrorCode.InternalError : McpErrorCode.InvalidParams;
                var exception = new McpProtocolException(e.Message, code);
                var body = JsonSerializer.SerializeToElement(error);
                exception.Data["ok"] = body.GetProperty("ok").Clone();
                exception.Data["error"] = body.GetProperty("error").Clone();
                throw exception;
            }
        };
}
