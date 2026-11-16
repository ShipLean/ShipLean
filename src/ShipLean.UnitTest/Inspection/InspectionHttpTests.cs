using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Inspection;

namespace ShipLean.UnitTest.Inspection;

[TestFixture]
public class InspectionHttpTests
{
    private sealed class Capture : HttpMessageHandler
    {
        public HttpRequestMessage? Seen { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Seen = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }

    private static async Task<HttpRequestMessage> SendThroughAsync(string client, InspectionOptions options, string? tenantOverride = null)
    {
        var capture = new Capture();
        var services = new ServiceCollection();
        services.AddSingleton(options);
        InspectionHttp.Add(services);
        services.AddHttpClient(InspectionHttp.TempoClient).ConfigurePrimaryHttpMessageHandler(() => capture);
        services.AddHttpClient(InspectionHttp.LokiClient).ConfigurePrimaryHttpMessageHandler(() => capture);
        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);
        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(client);
        using var request = new HttpRequestMessage(HttpMethod.Get, "http://store.example/ready");
        if (tenantOverride is not null)
        {
            request.Options.Set(InspectionHttp.TenantOverride, tenantOverride);
        }
        (await http.SendAsync(request)).Dispose();
        return capture.Seen!;
    }

    [Test]
    public async Task Tempo_WhenAuthIsBasic_ShouldSendBasicWithBase64OfUserAndPassword()
    {
        var seen = await SendThroughAsync(InspectionHttp.TempoClient, new InspectionOptions { TempoAuth = "basic:alice:s3cret" });

        seen.Headers.Authorization!.ToString().ShouldBe("Basic YWxpY2U6czNjcmV0");
    }

    [Test]
    public async Task Loki_WhenAuthIsBearer_ShouldSendBearerToken()
    {
        var seen = await SendThroughAsync(InspectionHttp.LokiClient, new InspectionOptions { LokiAuth = "bearer:tok123" });

        seen.Headers.Authorization!.ToString().ShouldBe("Bearer tok123");
    }

    [Test]
    public async Task Tempo_WhenAuthIsNone_ShouldSendNoAuthorization()
    {
        var seen = await SendThroughAsync(InspectionHttp.TempoClient, new InspectionOptions { TempoAuth = "none" });

        seen.Headers.Authorization.ShouldBeNull();
    }

    [Test]
    public async Task Tempo_WhenInstanceHasATenant_ShouldSendItAsScopeOrgId()
    {
        var seen = await SendThroughAsync(InspectionHttp.TempoClient, new InspectionOptions { TempoTenant = "team-a", LokiTenant = "other" });

        seen.Headers.GetValues(InspectionHttp.TenantHeader).ShouldBe(["team-a"]);
    }

    [Test]
    public async Task Loki_WhenTheRequestOverridesTheTenant_ShouldSendTheOverride()
    {
        var seen = await SendThroughAsync(InspectionHttp.LokiClient, new InspectionOptions { LokiTenant = "team-a" }, "project-tenant");

        seen.Headers.GetValues(InspectionHttp.TenantHeader).ShouldBe(["project-tenant"]);
    }

    [Test]
    public async Task Tempo_WhenThereIsNoTenant_ShouldSendNoScopeOrgId()
    {
        var seen = await SendThroughAsync(InspectionHttp.TempoClient, new InspectionOptions());

        seen.Headers.Contains(InspectionHttp.TenantHeader).ShouldBeFalse();
    }

    [Test]
    public async Task Tempo_WhenOptionsDefineAUrl_ShouldUseItAsBaseAddress()
    {
        var capture = new Capture();
        var services = new ServiceCollection();
        services.AddSingleton(new InspectionOptions { TempoUrl = "http://tempo.example/api" });
        InspectionHttp.Add(services);
        services.AddHttpClient(InspectionHttp.TempoClient).ConfigurePrimaryHttpMessageHandler(() => capture);
        var provider = new DefaultServiceProviderFactory().CreateServiceProvider(services);

        var http = provider.GetRequiredService<IHttpClientFactory>().CreateClient(InspectionHttp.TempoClient);

        http.BaseAddress.ShouldBe(new Uri("http://tempo.example/api/"));
    }
}
