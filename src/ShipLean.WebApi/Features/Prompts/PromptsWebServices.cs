using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;

namespace ShipLean.WebApi.Features.Prompts;

public sealed class PromptsWebServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IBuiltInPrompts, BuiltInPrompts>();
        services.AddScoped<IConnectedAgent, RequestConnectedAgent>();
    }
}
