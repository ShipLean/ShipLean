using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.Search.Index;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Search.Index.Service;

// The pass tests count exact passes, so the test host must not index in the background unless a fixture adds the service.
[TestFixture]
public class SearchIndexHostedServiceHostDefaultTests : ApiFixture
{
    [Test]
    public void Host_ShouldNotRunSearchIndexHostedServiceByDefault()
    {
        Host.Services.GetServices<IHostedService>().OfType<SearchIndexHostedService>().ShouldBeEmpty();
    }
}
