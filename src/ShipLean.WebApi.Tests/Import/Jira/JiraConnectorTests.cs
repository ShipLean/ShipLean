using System.Net;
using Microsoft.Extensions.Configuration;
using ShipLean.Application.Features.Import.Jira;
using Shouldly;

namespace ShipLean.WebApi.Tests.Import.Jira;

// Runs alone: one test sets the process-wide default proxy.
[TestFixture]
[NonParallelizable]
public sealed class JiraConnectorTests
{
    [Test]
    public async Task Connector_WhenHostResolvesToLoopback_ShouldRefuseToConnect()
    {
        using var handler = JiraConnector.CreateHandler(new ConfigurationBuilder().Build());
        using var client = new HttpClient(handler);

        var error = await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("https://localhost:9/"));

        error.InnerException.ShouldBeOfType<JiraBlockedAddressException>();
    }

    [Test]
    public async Task Connector_WhenLocalEndpointsAllowed_ShouldTryToConnect()
    {
        var allowed = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { [JiraEndpointPolicy.AllowLocalKey] = "true" }).Build();
        using var handler = JiraConnector.CreateHandler(allowed);
        using var client = new HttpClient(handler);

        var error = await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync($"http://127.0.0.1:{LoopbackPorts.Closed()}/"));

        error.InnerException.ShouldNotBeOfType<JiraBlockedAddressException>();
    }

    [Test]
    public async Task Connector_WhenAProxyIsConfigured_ShouldNotUseItAndStillRefuseALoopbackHost()
    {
        var original = HttpClient.DefaultProxy;
        HttpClient.DefaultProxy = new WebProxy("http://198.51.100.7:3128");
        try
        {
            using var handler = JiraConnector.CreateHandler(new ConfigurationBuilder().Build());
            using var client = new HttpClient(handler);

            var error = await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("http://localhost:9/latest/meta-data"));

            handler.UseProxy.ShouldBeFalse();
            error.InnerException.ShouldBeOfType<JiraBlockedAddressException>();
        }
        finally
        {
            HttpClient.DefaultProxy = original;
        }
    }
}
