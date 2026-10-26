using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Features.Agents.Addresses;

public sealed class AgentAddressesServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IAgentAddresses>(sp => new AgentAddressesReader(
            sp.GetRequiredService<NpgsqlDataSource>(), () => sp.GetRequiredService<IHttpContextAccessor>().HttpContext));
    }
}
