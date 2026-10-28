using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.AgentDispatch;

[TestFixture]
public class AgentDispatchHostTests : ApiFixture
{
    [Test]
    public void Host_ShouldRunTheAgentDispatchSubscriber()
    {
        Host.Services.GetServices<IHostedService>().OfType<AgentDispatchSubscriber>().ShouldHaveSingleItem();
    }

    [Test]
    public void AddServices_ShouldRegisterTheDeliveryService()
    {
        var services = new ServiceCollection();
        new AgentDispatchServices().AddServices(services, new ConfigurationBuilder().Build());

        services.ShouldContain(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(AgentDeliveryService));
    }
}
