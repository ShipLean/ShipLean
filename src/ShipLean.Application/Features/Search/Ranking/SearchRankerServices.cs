using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Ranking;

public sealed class SearchRankerServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<ISearchRanker, SearchRanker>();
}
