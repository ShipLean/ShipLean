using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Import.Jira;
using ShipLean.TestSupport.Jira;

namespace ShipLean.WebApi.Tests.Import.Jira;

// Pins the in-process Jira stub under the app's named Jira client, so the real client runs without a network.
internal static class JiraStubHost
{
    public static void Pin(IServiceCollection services, JiraStubHandler stub)
        => services.AddHttpClient(JiraTransport.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => stub);

    // The app reads its settings on every call, so a test host takes a switch added to its live configuration.
    public static void AllowLocalEndpoints(IServiceProvider services)
        => ((IConfigurationBuilder)services.GetRequiredService<IConfiguration>()).AddInMemoryCollection(new Dictionary<string, string?> { [JiraEndpointPolicy.AllowLocalKey] = "true" });
}
