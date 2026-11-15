using System.Text;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.Inspection;
using ShipLean.Application.Features.Inspection.TempoLoki;
using ShipLean.Contracts.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

// The headers the instance's auth and tenant settings put on what the Tempo and Loki sources send, seen by a stub handler.
[TestFixture(TestName = "Tempo and Loki auth and tenant headers")]
public sealed class TempoAuthTests
{
    private static async Task<StubStores> RunAsync(InspectionOptions options, string? tenantOverride)
    {
        var stores = new StubStores();
        var clients = StoreClients.Create(options, stores);
        var cache = new RunLinkCache(new InMemoryRunLinkStore(), new FakeTimeProvider());
        var finder = new TempoLokiRunFinder(new TempoRunFinder(clients, options), new LokiRunFinder(clients, options), cache, options);
        var now = new FakeTimeProvider().GetUtcNow();
        await finder.FindAsync(new RunQuery("SHIP", "SHIP-1", now.AddHours(-1), now, tenantOverride, null), CancellationToken.None);
        return stores;
    }

    [Test]
    public async Task Find_WithBasicAuth_SendsBasicAuthorizationToBothStores()
    {
        var options = new InspectionOptions { TempoUrl = "http://tempo.test", LokiUrl = "http://loki.test", TempoAuth = "basic:me:secret", LokiAuth = "basic:me:secret" };

        var stores = await RunAsync(options, null);

        stores.Requests.ShouldNotBeEmpty();
        stores.Requests.ShouldAllBe(r => r.Headers.Authorization != null && r.Headers.Authorization.Scheme == "Basic" && r.Headers.Authorization.Parameter == Convert.ToBase64String(Encoding.UTF8.GetBytes("me:secret")));
    }

    [Test]
    public async Task Find_WithBearerAuth_SendsBearerAuthorization()
    {
        var options = new InspectionOptions { TempoUrl = "http://tempo.test", LokiUrl = "http://loki.test", TempoAuth = "bearer:tok", LokiAuth = "bearer:tok" };

        var stores = await RunAsync(options, null);

        stores.Requests.ShouldNotBeEmpty();
        stores.Requests.ShouldAllBe(r => r.Headers.Authorization != null && r.Headers.Authorization.Scheme == "Bearer" && r.Headers.Authorization.Parameter == "tok");
    }

    [Test]
    public async Task Find_WithAnInstanceTenant_SendsItAsScopeOrgId()
    {
        var options = new InspectionOptions { TempoUrl = "http://tempo.test", LokiUrl = "http://loki.test", TempoTenant = "t-tempo", LokiTenant = "t-loki" };

        var stores = await RunAsync(options, null);

        stores.Requests.Where(r => r.RequestUri!.Host == "tempo.test").ShouldAllBe(r => r.Headers.GetValues(InspectionHttp.TenantHeader).Single() == "t-tempo");
        stores.Requests.Where(r => r.RequestUri!.Host == "loki.test").ShouldAllBe(r => r.Headers.GetValues(InspectionHttp.TenantHeader).Single() == "t-loki");
    }

    [Test]
    public async Task Find_WithAProjectTenantOverride_SendsTheOverrideInsteadOfTheInstanceTenant()
    {
        var options = new InspectionOptions { TempoUrl = "http://tempo.test", LokiUrl = "http://loki.test", TempoTenant = "t-tempo", LokiTenant = "t-loki" };

        var stores = await RunAsync(options, "project-tenant");

        stores.Requests.ShouldNotBeEmpty();
        stores.Requests.ShouldAllBe(r => r.Headers.GetValues(InspectionHttp.TenantHeader).Single() == "project-tenant");
    }
}
