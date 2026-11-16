using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace ShipLean.Application.Features.Inspection;

// The named HttpClients the Tempo and Loki sources use (K1): base address, Authorization and X-Scope-OrgID come from InspectionOptions when the
// request is sent. A source passes the project's tenant override per request with TenantOverride.
public static class InspectionHttp
{
    public const string TempoClient = "inspection-tempo";

    public const string LokiClient = "inspection-loki";

    private const char Slash = (char)47;

    // No single request to a store waits longer than this (the HttpClient default is 100 s).
    public static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(15);

    public const string TenantHeader = "X-Scope-OrgID";

    public static readonly HttpRequestOptionsKey<string> TenantOverride = new("shiplean.inspection.tenant");

    public static void Add(IServiceCollection services)
    {
        services.AddTransient(sp => new InspectionAuthHandler(() => sp.GetRequiredService<InspectionOptions>().TempoAuth, () => sp.GetRequiredService<InspectionOptions>().TempoTenant));
        services.AddTransient(sp => new LokiAuthHandler(() => sp.GetRequiredService<InspectionOptions>().LokiAuth, () => sp.GetRequiredService<InspectionOptions>().LokiTenant));
        services.AddHttpClient(TempoClient, (sp, client) => SetBase(client, sp.GetRequiredService<InspectionOptions>().TempoUrl)).AddHttpMessageHandler<InspectionAuthHandler>();
        services.AddHttpClient(LokiClient, (sp, client) => SetBase(client, sp.GetRequiredService<InspectionOptions>().LokiUrl)).AddHttpMessageHandler<LokiAuthHandler>();
    }

    private static void SetBase(HttpClient client, string? url)
    {
        client.Timeout = RequestTimeout;
        if (!string.IsNullOrWhiteSpace(url))
        {
            client.BaseAddress = new Uri(url.TrimEnd(Slash) + Slash);
        }
    }
}

// Sets Authorization (none | basic:<user>:<password> | bearer:<token>) and X-Scope-OrgID (the override on the request, else the instance tenant).
public class InspectionAuthHandler(Func<string?> auth, Func<string?> tenant) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var authorization = Authorization(auth());
        if (authorization is not null)
        {
            request.Headers.Authorization = authorization;
        }
        var org = request.Options.TryGetValue(InspectionHttp.TenantOverride, out var over) && !string.IsNullOrWhiteSpace(over) ? over : tenant();
        if (!string.IsNullOrWhiteSpace(org))
        {
            request.Headers.Remove(InspectionHttp.TenantHeader);
            request.Headers.Add(InspectionHttp.TenantHeader, org);
        }
        return base.SendAsync(request, cancellationToken);
    }

    private static AuthenticationHeaderValue? Authorization(string? value)
    {
        if (value is null || value == "none")
        {
            return null;
        }
        if (value.StartsWith("bearer:", StringComparison.Ordinal))
        {
            return new AuthenticationHeaderValue("Bearer", value["bearer:".Length..]);
        }
        if (value.StartsWith("basic:", StringComparison.Ordinal))
        {
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(value["basic:".Length..])));
        }
        return null;
    }
}

// A second type so the two named clients each get their own handler registration.
public sealed class LokiAuthHandler(Func<string?> auth, Func<string?> tenant) : InspectionAuthHandler(auth, tenant)
{
}
