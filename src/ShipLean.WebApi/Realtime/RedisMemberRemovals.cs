using System.Text.Json;
using StackExchange.Redis;

namespace ShipLean.WebApi.Realtime;

// IMemberRemovals across instances: drops the user's connections here at once, then publishes { projectId, userId } on
// RedisRealtime.MembersRemovedChannel, which every instance (this one too, harmlessly) handles by dropping its own.
// Redis pub/sub is at most once, and a failed publish is only logged: HubSessionSweep drops what a lost message left
// within a minute.
public sealed class RedisMemberRemovals(RedisRealtime redis, ProjectGroupMembers members, ILogger<RedisMemberRemovals> logger)
    : IMemberRemovals, IHostedService
{
    private sealed record Removal(Guid ProjectId, Guid UserId);

    private ChannelMessageQueue? _queue;

    public async Task RemovedAsync(Guid projectId, Guid userId)
    {
        await members.RemoveUserAsync(projectId, userId);
        try
        {
            var subscriber = await redis.SubscriberAsync();
            await subscriber.PublishAsync(redis.MembersRemovedChannel, JsonSerializer.Serialize(new Removal(projectId, userId), JsonSerializerOptions.Web));
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not tell the other instances that user {UserId} left project {ProjectId}; the hub sweep drops their connections.",
                userId, projectId);
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var subscriber = await redis.SubscriberAsync();
        _queue = await subscriber.SubscribeAsync(redis.MembersRemovedChannel);
        _queue.OnMessage(HandleAsync);
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_queue != null)
        {
            await _queue.UnsubscribeAsync();
        }
    }

    private async Task HandleAsync(ChannelMessage message)
    {
        try
        {
            var removal = JsonSerializer.Deserialize<Removal>(message.Message.ToString(), JsonSerializerOptions.Web)!;
            await members.RemoveUserAsync(removal.ProjectId, removal.UserId);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not handle a member removal from another instance; the hub sweep drops their connections.");
        }
    }
}
