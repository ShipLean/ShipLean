using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Search.Ranking.Semantic;

// The vector cache is shared by every request, so it is a singleton; the leg is registered with the other legs (SearchRankingServices).
public sealed class SemanticServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<SearchVectorCache>();
}
