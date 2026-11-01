using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Retros.Theming;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Search;

namespace ShipLean.UnitTest.Retros;

[TestFixture]
public class RetroThemerServicesTests
{
    [Test]
    public async Task Register_WhenSwitchUnset_ShouldGiveAThemerInPickMode()
    {
        await using var provider = Build(null);

        provider.GetRequiredService<IRetroThemer>().Mode.ShouldBe(RetroThemeMode.Pick);
    }

    [Test]
    public async Task Register_WhenSwitchInvalid_ShouldThrowOnResolve()
    {
        await using var provider = Build("maybe");

        Should.Throw<InvalidOperationException>(() => provider.GetRequiredService<IRetroThemer>()).Message.ShouldContain("SHIPLEAN_RETRO_AUTO_THEMES");
    }

    private static ServiceProvider Build(string? value)
    {
        var settings = new Dictionary<string, string?> { [RetroThemerServices.AutoThemesKey] = value };
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(settings).Build());
        services.AddSingleton(new NpgsqlDataSourceBuilder("Host=localhost").Build());
        services.AddSingleton<IEmbedder>(new NoEmbedder("off"));
        services.AddSingleton<PickRetroThemer>();
        RetroThemerServices.Register(services);
        return (ServiceProvider)new DefaultServiceProviderFactory().CreateServiceProvider(services);
    }
}
