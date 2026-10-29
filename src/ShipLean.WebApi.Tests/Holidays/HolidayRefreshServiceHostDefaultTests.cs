using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.Holidays;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Holidays;

// No test host may call the internet, so the holiday job is off unless a fixture adds it back.
[TestFixture]
public class HolidayRefreshServiceHostDefaultTests : ApiFixture
{
    [Test]
    public void Host_ShouldNotRunHolidayRefreshServiceByDefault()
    {
        Host.Services.GetServices<IHostedService>().OfType<HolidayRefreshService>().ShouldBeEmpty();
    }
}
