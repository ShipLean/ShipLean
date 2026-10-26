using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Guide;

// The prompt error filter's two branches, with prompts that throw: an ApiException keeps its code (internal_error is -32603, any
// other -32602), and anything else is left to the SDK, whose answer says nothing about what went wrong.
[TestFixture]
public class PromptErrorFilterTests : McpFixture
{
    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddMcpServer().WithPrompts<ThrowingPrompts>();
    };

    [Test]
    public async Task PromptErrorFilter_WithInternalApiException_ShouldKeepTheCodeAsInternalError()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await GetAsync(token, "throw_internal");

        reply.Json().GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32603);
        var data = reply.Json().GetProperty("error").GetProperty("data");
        data.GetProperty("ok").GetBoolean().ShouldBeFalse();
        data.GetProperty("error").GetProperty("code").GetString().ShouldBe("internal_error");
    }

    [Test]
    public async Task PromptErrorFilter_WithOtherException_ShouldAnswerInternalErrorWithoutDetail()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await GetAsync(token, "throw_other");

        reply.Json().GetProperty("error").GetProperty("code").GetInt32().ShouldBe(-32603);
        reply.Json().GetProperty("error").TryGetProperty("data", out _).ShouldBeFalse();
        reply.Body.ShouldNotContain("secret");
        reply.Body.ShouldNotContain("internal_error");
    }

    private Task<McpReply> GetAsync(string token, string prompt)
        => PostAsync(token, $$$$"""{"jsonrpc":"2.0","id":1,"method":"prompts/get","params":{"name":"{{{{prompt}}}}","arguments":{}}}""");

    [McpServerPromptType]
    public sealed class ThrowingPrompts
    {
        [McpServerPrompt(Name = "throw_internal")]
        public string ThrowInternal() => throw ApiException.Internal();

        [McpServerPrompt(Name = "throw_other")]
        public string ThrowOther() => throw new InvalidOperationException("secret detail");
    }
}
