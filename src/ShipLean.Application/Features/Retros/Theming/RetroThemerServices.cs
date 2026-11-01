using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using Npgsql;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Retros.Theming;

// PickRetroThemer is always registered as itself, so a person's named theme can use it in any mode. Register is public so a
// test fixture can opt back into the production themer over the host's pick default.
// Register resolves RetroThemer, which is auto when the embedder is semantic and delegates to PickRetroThemer otherwise.
public sealed class RetroThemerServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<PickRetroThemer>();
        Register(services);
    }

    public static void Register(IServiceCollection services)
        => services.AddSingleton<IRetroThemer>(sp => new RetroThemer(sp.GetRequiredService<NpgsqlDataSource>(), sp.GetRequiredService<IEmbedder>(), sp.GetRequiredService<PickRetroThemer>()));
}
