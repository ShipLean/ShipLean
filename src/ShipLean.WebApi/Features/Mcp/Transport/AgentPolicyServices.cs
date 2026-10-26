using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using ModelContextProtocol.Authentication;
using ShipLean.Contracts;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// How /mcp lets an agent in. The policy accepts only the bearer scheme (the cookie is never consulted there). The SDK's
// resource-metadata scheme serves /.well-known/oauth-protected-resource[/mcp] to anyone; the 401 challenge itself is the
// bearer scheme's, because the SDK's cannot carry the scope. The rate-limit policy limits writing tool calls per user: the
// scope gate marks a call as writing, and the limiter runs after it.
public sealed class AgentPolicyServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Read at resolve time, so a test host's setting counts. The startup log resolves it as the host starts.
        services.AddSingleton(sp => McpGatewaySettings.Parse(sp.GetRequiredService<IConfiguration>()));
        services.AddHostedService<McpGatewayStartupLog>();
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, AgentBearerHandler>(AgentPolicy.Scheme, _ => { })
            .AddMcp(AgentPolicy.ResourceMetadataScheme, "Protected resource metadata", options =>
                options.Events.OnResourceMetadataRequest = async context =>
                {
                    var addresses = await context.HttpContext.RequestServices.GetRequiredService<AgentBaseUrl>()
                        .ReadAsync(context.HttpContext.Request, context.HttpContext.RequestAborted);
                    context.ResourceMetadata = new ProtectedResourceMetadata
                    {
                        Resource = addresses.Resource,
                        AuthorizationServers = [addresses.Issuer],
                        BearerMethodsSupported = ["header"],
                        ScopesSupported = [McpScopes.Read, McpScopes.Write, McpScopes.Plan],
                    };
                });
        services.AddAuthorizationBuilder()
            .AddPolicy(AgentPolicy.Name, policy => policy.AddAuthenticationSchemes(AgentPolicy.Scheme).RequireAuthenticatedUser());
        services.AddRateLimiter(options => options.AddPolicy(AgentPolicy.Name, context =>
            context.Items[McpPipeline.WritingUserItem] is string userId
                ? RateLimitPartition.GetFixedWindowLimiter(userId, _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = AgentPolicy.WritesPerMinutePerUser,
                    Window = TimeSpan.FromMinutes(1),
                })
                : RateLimitPartition.GetNoLimiter(string.Empty)));
    }
}
