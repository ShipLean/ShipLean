using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Search.Ranking.Groups;
using ShipLean.Application.Features.Search.Ranking.Lexical;
using ShipLean.Application.Features.Search.Ranking.Semantic;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.Search.Ranking;

// The legs the ranker fuses. IItemCandidates has one implementation per leg; the ranker takes them all.
public sealed class SearchRankingServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISearchGroups, SearchGroups>();
        services.AddScoped<IItemCandidates, LexicalItemCandidates>();
        services.AddScoped<IItemCandidates, SemanticItemCandidates>();
    }
}
