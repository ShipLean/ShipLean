using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Gate;

// The call-tool filter is the backstop behind the gate. The shifty tools tell the gate one scope and the filter another, so
// the gate lets through a call the SDK-side check must still stop: the tool never runs and the answer is a forbidden McpError.
[TestFixture]
public class ToolScopeFilterTests : McpFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        AddShifty(services, "shifty_to_write", McpScopes.Read, McpScopes.Write);
        AddShifty(services, "shifty_write_to_plan", McpScopes.Write, McpScopes.Plan);
        AddShifty(services, "shifty_to_none", McpScopes.Read, null);
        AddShifty(services, "shifty_steady_read", McpScopes.Read, McpScopes.Read);
        AddShifty(services, "shifty_steady_write", McpScopes.Write, McpScopes.Write);
    };

    [Test]
    public async Task ScopeFilter_WhenTheSdkSideNeedsAScopeTheTokenLacks_ShouldAnswerForbiddenMcpError()
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);

        var reply = await CallToolAsync(token, "shifty_to_write");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("forbidden");
        Log.Runs("Shifty").ShouldBe(0);
    }

    [Test]
    public async Task ScopeFilter_WhenACountedWriteNeedsAScopeTheTokenLacks_ShouldAnswerForbiddenMcpError()
    {
        var token = await TokenAsync(TestUser.Mei, ReadWrite);

        var reply = await CallToolAsync(token, "shifty_write_to_plan");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("forbidden");
        Log.Runs("Shifty").ShouldBe(0);
    }

    [Test]
    public async Task ScopeFilter_WhenAWriteSlippedPastTheGateUncounted_ShouldAnswerForbiddenMcpError()
    {
        var token = await TokenAsync(TestUser.Mei, ReadWrite);

        var reply = await CallToolAsync(token, "shifty_to_write");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("forbidden");
        Log.Runs("Shifty").ShouldBe(0);
    }

    [Test]
    public async Task ScopeFilter_WhenTheSdkSideFindsNoScope_ShouldAnswerForbiddenMcpError()
    {
        var token = await TokenAsync(TestUser.Mei, ReadWritePlan);

        var reply = await CallToolAsync(token, "shifty_to_none");

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("forbidden");
        Log.Runs("Shifty").ShouldBe(0);
    }

    [Test]
    public async Task ScopeFilter_WhenGateAndSdkAgree_ShouldRunTheTool()
    {
        var reader = await TokenAsync(TestUser.Mei, ReadOnly);
        var writer = await TokenAsync(TestUser.Mei, ReadWrite);

        var read = await CallToolAsync(reader, "shifty_steady_read");
        var write = await CallToolAsync(writer, "shifty_steady_write");

        read.IsToolError().ShouldBeFalse();
        write.IsToolError().ShouldBeFalse();
        Log.Runs("Shifty").ShouldBe(2);
    }

    private static void AddShifty(IServiceCollection services, string name, string first, string? later)
        => services.AddSingleton<McpServerTool>(sp => ShiftyTool.Create(name, sp.GetRequiredService<ProbeLog>(), () => sp.GetRequiredService<IHttpContextAccessor>().HttpContext!.Items, first, later));
}
