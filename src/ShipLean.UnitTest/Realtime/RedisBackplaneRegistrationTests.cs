using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.StackExchangeRedis;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShipLean.Application.Events;
using ShipLean.Contracts.Events;
using ShipLean.WebApi.Realtime;

namespace ShipLean.UnitTest.Realtime;

// The hub lifetime manager, presence tracker and member removals exactly as RealtimeServices registers them.
// Registration never connects to Redis, so no case needs a server.
public sealed class RedisBackplaneRegistrationTests
{
    [Test]
    public void AddServices_WhenRedisConnectionUnset_ShouldKeepTheInProcessLifetimeManager()
    {
        LifetimeManagerFor(null).ShouldBe(typeof(DefaultHubLifetimeManager<>));
    }

    [TestCase("")]
    [TestCase("   ")]
    public void AddServices_WhenRedisConnectionBlank_ShouldKeepTheInProcessLifetimeManager(string value)
    {
        LifetimeManagerFor(value).ShouldBe(typeof(DefaultHubLifetimeManager<>));
    }

    [Test]
    public void AddServices_WhenRedisConnectionSet_ShouldUseTheRedisBackplaneWithOurChannelPrefix()
    {
        var services = Register("redis:6379,abortConnect=false");

        services.Last(d => d.ServiceType == typeof(HubLifetimeManager<>)).ImplementationType
            .ShouldBe(typeof(RedisHubLifetimeManager<>));
        using var provider = (ServiceProvider)new DefaultServiceProviderFactory().CreateServiceProvider(services);
        var options = provider.GetRequiredService<IOptions<RedisOptions>>().Value;
        options.Configuration.ChannelPrefix.ToString().ShouldBe("shiplean");
        options.Configuration.EndPoints.Single().ToString().ShouldBe("Unspecified/redis:6379");
    }

    [Test]
    public async Task AddServices_WhenRedisConnectionNamesAChannelPrefix_ShouldKeepItForTheBackplaneAndOurKeys()
    {
        var services = Register("redis:6379,abortConnect=false,channelPrefix=staging");

        await using var provider = (ServiceProvider)new DefaultServiceProviderFactory().CreateServiceProvider(services);
        provider.GetRequiredService<IOptions<RedisOptions>>().Value.Configuration.ChannelPrefix.ToString().ShouldBe("staging");
        provider.GetRequiredService<RedisRealtime>().Prefix.ShouldBe("staging");
        provider.GetRequiredService<RedisRealtime>().MembersRemovedChannel.ToString().ShouldBe("staging:members-removed");
    }

    [Test]
    public void AddServices_WhenRedisConnectionUnset_ShouldKeepPresenceAndRemovalsInProcess()
    {
        var services = Register(null);

        ImplementationOf<IPresenceTracker>(services).ShouldBe(typeof(PresenceTracker));
        ImplementationOf<IMemberRemovals>(services).ShouldBe(typeof(LocalMemberRemovals));
        services.ShouldNotContain(d => d.ServiceType == typeof(RedisRealtime));
    }

    [Test]
    public async Task AddServices_WhenRedisConnectionSet_ShouldSharePresenceAndRemovalsThroughRedis()
    {
        var services = Register("redis:6379,abortConnect=false");
        // Registered elsewhere in the app (the bus by its module, the group members by attribute).
        services.AddSingleton<IProjectEvents, InMemoryProjectEvents>();
        services.AddSingleton<ProjectGroupMembers>();

        await using var provider = (ServiceProvider)new DefaultServiceProviderFactory().CreateServiceProvider(services);
        provider.GetRequiredService<IPresenceTracker>().ShouldBeOfType<RedisPresenceTracker>();
        provider.GetRequiredService<IMemberRemovals>().ShouldBeOfType<RedisMemberRemovals>();
        services.ShouldContain(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(PresenceHeartbeat));
        provider.GetRequiredService<RedisRealtime>().Prefix.ShouldBe("shiplean");
    }

    private static Type? ImplementationOf<T>(ServiceCollection services)
        => services.Single(d => d.ServiceType == typeof(T)).ImplementationType;

    private static Type? LifetimeManagerFor(string? value)
        => Register(value).Last(d => d.ServiceType == typeof(HubLifetimeManager<>)).ImplementationType;

    private static ServiceCollection Register(string? value)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [RealtimeServices.RedisConnectionKey] = value })
            .Build();
        var services = new ServiceCollection();
        // What the host provides, so the Redis services can be resolved.
        services.AddSingleton<ILoggerFactory, NullLoggerFactory>();
        services.AddSingleton(typeof(ILogger<>), typeof(Logger<>));
        new RealtimeServices().AddServices(services, configuration);
        return services;
    }
}
