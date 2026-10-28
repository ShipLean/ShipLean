using System.Net;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application;
using ShipLean.Application.Database;
using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Features.LinearApi;
using ShipLean.WebApi.Infrastructure;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Tests;

[TestFixture]
public class ModuleScanTests : ApiFixture
{
    [Test]
    public void Scan_WhenHealthModuleInProductionAssembly_ShouldFindIt()
    {
        var modules = ModuleScan.Find<IEndpointModule>([ApiMarker.Assembly, ApplicationMarker.Assembly]);

        modules.Select(m => m.GetType()).ShouldContain(typeof(HealthEndpoints));
    }

    // Feature endpoints are controllers; IEndpointModule is only for what MVC does not map (health, the sign-in page, the hub, the Linear-compatible GraphQL endpoint).
    [Test]
    public void Scan_WhenEndpointModulesFound_ShouldBeOnlyTheNonMvcEndpoints()
    {
        var modules = ModuleScan.Find<IEndpointModule>([ApiMarker.Assembly, ApplicationMarker.Assembly]);

        modules.Select(m => m.GetType()).ShouldBe([typeof(HealthEndpoints), typeof(LoginPageEndpoints), typeof(RealtimeEndpoints), typeof(LinearApiEndpoints)], ignoreOrder: true);
    }

    [Test]
    public void Endpoints_UnderApi_ShouldAllBeControllerActionsExceptHealthAndTheFallback()
    {
        var notControllers = Host.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>()
            .Where(e => ("/" + e.RoutePattern.RawText?.TrimStart('/')).StartsWith("/api/", StringComparison.Ordinal))
            .Where(e => e.Metadata.GetMetadata<ControllerActionDescriptor>() is null)
            .Select(e => e.RoutePattern.RawText);

        notControllers.ShouldBe(["/api/health", "/api/{**path}"], ignoreOrder: true);
    }

    [Test]
    public void Scan_WhenServiceModuleInApplicationAssembly_ShouldFindIt()
    {
        var modules = ModuleScan.Find<IServiceModule>([ApiMarker.Assembly, ApplicationMarker.Assembly]);

        modules.Select(m => m.GetType()).ShouldContain(typeof(DatabaseServices));
    }

    [Test]
    public async Task Program_WhenModulesScanned_ShouldMapTheirEndpointsAndRegisterTheirServices()
    {
        var response = await Client.GetAsync("/api/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        Host.Services.GetRequiredService<NpgsqlDataSource>().ConnectionString.ShouldContain("shiplean_f_");
        Host.Services.GetRequiredService<ICurrentUser>().Id.ShouldBe(SeedIds.Users.Joel);
    }
}
