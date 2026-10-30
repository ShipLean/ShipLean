using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Holidays;

namespace ShipLean.Application.Features.Holidays;

// The holiday store, the Nager.Date client over a named HttpClient, and the job that refreshes the store. Test hosts remove
// the job by default (ShipLeanHost). The sprint capacity feature registers IHolidaysChanged.
public sealed class HolidaysServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<HolidaySettings>();
        services.AddHttpClient(NagerClient.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(20));
        services.AddSingleton<NagerClient>();
        services.AddSingleton<HolidayRefresher>();
        services.AddScoped<IPublicHolidays, PublicHolidayStore>();
        services.AddHostedService<HolidayRefreshService>();
    }
}
