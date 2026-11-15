using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Inspection;

namespace ShipLean.WebApi.Tests.Inspection.TempoLoki.Find;

// Answers every Tempo and Loki request with a canned JSON body and keeps what was asked, so tests see the requests the sources send.
public sealed class StubStores : HttpMessageHandler
{
    private readonly List<HttpRequestMessage> seen = [];

    public IReadOnlyList<HttpRequestMessage> Requests => seen;

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        seen.Add(request);
        var path = request.RequestUri!.AbsolutePath;
        var body = "{}";
        if (path.EndsWith("/api/search", StringComparison.Ordinal))
        {
            body = "{\"traces\":[]}";
        }
        else if (path.Contains("/loki/", StringComparison.Ordinal))
        {
            body = "{\"data\":{\"result\":[]}}";
        }
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, "application/json") });
    }
}

// The named Tempo and Loki clients, built the way the app builds them, optionally answered by a stub instead of the network.
public static class StoreClients
{
    public static IHttpClientFactory Create(InspectionOptions options, HttpMessageHandler? stub = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(options);
        InspectionHttp.Add(services);
        if (stub is not null)
        {
            services.AddHttpClient(InspectionHttp.TempoClient).ConfigurePrimaryHttpMessageHandler(() => stub);
            services.AddHttpClient(InspectionHttp.LokiClient).ConfigurePrimaryHttpMessageHandler(() => stub);
        }
        return new DefaultServiceProviderFactory().CreateServiceProvider(services).GetRequiredService<IHttpClientFactory>();
    }
}
