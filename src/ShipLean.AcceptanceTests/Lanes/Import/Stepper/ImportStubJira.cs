using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Import.Jira;
using ShipLean.TestSupport.Jira;

namespace ShipLean.AcceptanceTests.Lanes.Import.Stepper;

// Pins the in-process Jira under the app's named Jira client, so the real client runs without a network.
internal static class ImportStubJira
{
    public static void Pin(IServiceCollection services, JiraStubHandler stub) =>
        services.AddHttpClient(JiraTransport.HttpClientName).ConfigurePrimaryHttpMessageHandler(() => stub);
}
