using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Prompts.Reads;
using ShipLean.Application.Features.Prompts.Servings;
using ShipLean.Application.Features.Prompts.Writes;
using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts;

public sealed class PromptsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPromptReads, DbPromptReads>();
        services.AddScoped<IPromptWrites, DbPromptWrites>();
        services.AddScoped<IPromptServingReads, DbPromptServingReads>();
    }
}
