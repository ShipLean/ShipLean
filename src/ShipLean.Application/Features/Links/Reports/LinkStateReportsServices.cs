using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.Reports;

public sealed class LinkStateReportsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<ILinkStateReports, LinkStateReports>();
}
