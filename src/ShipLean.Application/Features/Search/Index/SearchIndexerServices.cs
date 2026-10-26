using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index;

public sealed class SearchIndexerServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<ISearchIndexer, SearchIndexer>();
}
