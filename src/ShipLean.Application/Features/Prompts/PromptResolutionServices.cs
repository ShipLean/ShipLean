using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Prompts.Resolve;
using ShipLean.Application.Features.Prompts.Servings;
using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts;

public sealed class PromptResolutionServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPromptResolver, DbPromptResolver>();
        services.AddScoped<IPromptServingLog, DbPromptServingLog>();
    }
}
