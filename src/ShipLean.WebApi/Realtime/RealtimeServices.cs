using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ShipLean.Contracts;
using StackExchange.Redis;

namespace ShipLean.WebApi.Realtime;

public sealed class RealtimeServices : IServiceModule
{
    public const string HubPolicy = "ProjectHub";

    // Set when more than one instance runs: SignalR then fans group sends out through Redis, so an event published on
    // one instance reaches clients connected to the others, and presence and member removals are shared through the
    // same Redis. Unset, everything stays in process. Read here, not at resolve time: it decides what is registered.
    public const string RedisConnectionKey = "SHIPLEAN_REDIS_CONNECTION";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        var signalR = services.AddSignalR().AddHubOptions<ProjectHub>(options => options.AddFilter<HubSessionFilter>());
        var redis = configuration[RedisConnectionKey];
        if (!string.IsNullOrWhiteSpace(redis))
        {
            // The prefix keeps our channels apart from anything else on a shared Redis: the connection string's
            // channelPrefix when it sets one, else "shiplean". RedisRealtime prefixes its keys and channels the same.
            signalR.AddStackExchangeRedis(redis, options =>
                options.Configuration.ChannelPrefix = RedisChannel.Literal(RedisRealtime.PrefixOf(options.Configuration)));
            // A factory, not an instance, so the container disposes the connection.
            services.AddSingleton(_ => new RedisRealtime(redis));
            services.AddSingleton<RedisPresenceTracker>();
            services.AddSingleton<IPresenceTracker>(sp => sp.GetRequiredService<RedisPresenceTracker>());
            services.AddSingleton(PresenceHeartbeatTiming.Default);
            services.AddHostedService<PresenceHeartbeat>();
            services.AddSingleton<RedisMemberRemovals>();
            services.AddSingleton<IMemberRemovals>(sp => sp.GetRequiredService<RedisMemberRemovals>());
            services.AddHostedService(sp => sp.GetRequiredService<RedisMemberRemovals>());
        }
        else
        {
            services.AddSingleton<IPresenceTracker, PresenceTracker>();
            services.AddSingleton<IMemberRemovals, LocalMemberRemovals>();
        }

        services.AddHostedService<ProjectEventsRelay>();
        services.AddSingleton<HubSessionFilter>();
        services.AddSingleton<HubSessionSweep>();
        services.AddHostedService(sp => sp.GetRequiredService<HubSessionSweep>());
        services.AddSingleton<IAuthorizationHandler, ProjectHubHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(HubPolicy, policy => policy.RequireAuthenticatedUser().AddRequirements(new ProjectHubRequirement()));
    }
}
