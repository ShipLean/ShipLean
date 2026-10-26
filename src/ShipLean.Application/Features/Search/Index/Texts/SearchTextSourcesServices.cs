using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index.Texts;

// ISearchTextSource has one implementation per source kind; the writer asks for the one of its kind.
public sealed class SearchTextSourcesServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<ISearchTextSource, WorkItemTextSource>();
        services.AddScoped<ISearchTextSource, CommentTextSource>();
        services.AddScoped<ISearchTextSource, AttachmentTextSource>();
        services.AddScoped<ISearchTextSource, ProjectTextSource>();
        services.AddScoped<ISearchTextSource, SprintTextSource>();
    }
}
