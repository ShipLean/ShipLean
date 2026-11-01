using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using Npgsql;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Retros.Theming;

// PickRetroThemer is always registered as itself, so a person's named theme can use it in any mode. Register is public so a
// test fixture can opt back into the production themer over the host's pick default.
// Register resolves RetroThemer, which is auto only when SHIPLEAN_RETRO_AUTO_THEMES is on (default off) and the embedder is
// semantic, and delegates to PickRetroThemer otherwise. The switch is read when the themer is first resolved.
public sealed class RetroThemerServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<PickRetroThemer>();
        Register(services);
    }

    public const string AutoThemesKey = "SHIPLEAN_RETRO_AUTO_THEMES";

    public static void Register(IServiceCollection services)
        => services.AddSingleton<IRetroThemer>(sp => new RetroThemer(
            sp.GetRequiredService<NpgsqlDataSource>(),
            sp.GetRequiredService<IEmbedder>(),
            sp.GetRequiredService<PickRetroThemer>(),
            AutoThemesEnabled(sp.GetRequiredService<IConfiguration>())));

    private static bool AutoThemesEnabled(IConfiguration configuration)
        => configuration[AutoThemesKey]?.Trim().ToLowerInvariant() switch
        {
            null or "" or "off" => false,
            "on" => true,
            var other => throw new InvalidOperationException($"{AutoThemesKey} must be on or off, not \"{other}\"."),
        };
}
