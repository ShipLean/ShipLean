using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.Refresh;

public sealed class LinkCardsRefreshServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<ILinkCardsRefresh, LinkCardsRefresh>();
}
