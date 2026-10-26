using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agoda.IoC.NetCore;
using Serilog;
using ShipLean.Application;
using ShipLean.Contracts;
using ShipLean.WebApi;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Features.Mcp.Transport;
using ShipLean.WebApi.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Serilog replaces the logger factory, so levels live under Serilog:MinimumLevel
// rather than Logging:LogLevel. The console sink keeps `docker logs` working;
// Seq is posted to directly only when SHIPLEAN_SEQ_URL is set, so tests and local
// dev never reach for it. Configured from the service provider, not here, because
// WebApplicationFactory-based tests append their config overrides after this
// top-level code has run.
// preserveStaticLogger keeps each host's logger its own, so one test host being
// disposed can't close the static logger another is still writing through.
builder.Services.AddSerilog((services, logger) =>
{
    var configuration = services.GetRequiredService<IConfiguration>();
    logger.ReadFrom.Configuration(configuration)
        .Enrich.FromLogContext()
        .Enrich.WithProperty("Application", "shiplean")
        .WriteTo.Console();
    var seqUrl = configuration["SHIPLEAN_SEQ_URL"];
    if (!string.IsNullOrWhiteSpace(seqUrl))
    {
        logger.WriteTo.Seq(seqUrl, apiKey: configuration["SHIPLEAN_SEQ_API_KEY"]);
    }
}, preserveStaticLogger: true);

Assembly[] moduleAssemblies = [ApiMarker.Assembly, ApplicationMarker.Assembly];
builder.Services.AutoWireAssembly(moduleAssemblies, isMockMode: false);
foreach (var module in ModuleScan.Find<IServiceModule>(moduleAssemblies))
{
    module.AddServices(builder.Services, builder.Configuration);
}
// Framework registration: the AGENTS.md "no services.Add* in Program.cs" rule is
// about our own types. Tests swap in a FakeTimeProvider.
builder.Services.AddSingleton(TimeProvider.System);
// The pipeline below always runs authentication, authorization and the rate limiter. These register the framework
// services they need; the sign-in module adds the cookie scheme, policies and limits on top.
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(_ => { });
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(CamelCaseEnums()));
// Minimal API endpoints (the endpoint modules) serialize exactly like controllers: web defaults plus enums as camelCase
// names, the same casing as every persisted document and TypeScript union (`inProgress`, `oneTimePassword`).
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(CamelCaseEnums()));
// The MCP server for agents (#13): stateless Streamable HTTP, so each POST stands alone and runs as its token's user.
// Tools are the [McpServerToolType] classes in this assembly, internal ones included. Features/Mcp adds the bearer
// scheme, server instructions and the tool-call filter from its own modules (AddMcpServer is safe to call again there).
builder.Services.AddMcpServer()
    .WithHttpTransport(options => options.Stateless = true)
    .WithToolsFromAssembly(ApiMarker.Assembly);

var app = builder.Build();

// Behind a reverse proxy: the sign-in module configures which forwarded headers to trust.
app.UseForwardedHeaders();
// SPA assets ship inside wwwroot (built there by src/ShipLean.Client). Served before authorization, so the sign-in
// page's scripts and styles load without a session.
app.UseStaticFiles();
// The real-time hub's WebSocket transport.
app.UseWebSockets();
// Sign-in (Features/Auth): every endpoint needs a signed-in user unless it is marked AllowAnonymous.
app.UseAuthentication();
app.UseAuthorization();
// /mcp only (Features/Mcp): runs once the caller is known and the endpoint's policy has passed, before any tool.
app.UseWhen(context => context.Request.Path.StartsWithSegments("/mcp"), McpPipeline.Configure);
app.Use((context, next) =>
{
    CrossSiteWriteGuard.Check(context.Request);
    return next(context);
});
app.UseRateLimiter();

foreach (var module in ModuleScan.Find<IEndpointModule>(moduleAssemblies))
{
    module.MapEndpoints(app);
}
app.MapControllers();
app.MapMcp("/mcp")
    .RequireAuthorization(AgentPolicy.Name)
    .RequireRateLimiting(AgentPolicy.Name);
// Unknown routes fall back to the SPA for client-side routing, except under
// /api, which must keep returning 404 rather than the SPA shell.
app.MapFallback("/api/{**path}", () => Results.NotFound());
app.MapFallbackToFile("index.html");

await app.RunAsync();

static JsonStringEnumConverter CamelCaseEnums() => new(JsonNamingPolicy.CamelCase, allowIntegerValues: false);
