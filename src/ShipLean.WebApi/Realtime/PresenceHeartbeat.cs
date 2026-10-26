namespace ShipLean.WebApi.Realtime;

// With presence in Redis: every Period, refreshes this instance's heartbeat (a key that expires after Ttl) and takes
// the presence entries of every instance whose heartbeat has expired off their screens, so a killed instance leaves
// no ghost avatars. Every instance runs it; two cleaning up the same dead instance is harmless. On a clean stop the
// heartbeat is deleted, so the others clean up at their next beat whatever this one's disconnects did not.
public sealed class PresenceHeartbeat(RedisRealtime redis, RedisPresenceTracker presence, PresenceHeartbeatTiming timing, TimeProvider clock, ILogger<PresenceHeartbeat> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(timing.Period, clock);
        do
        {
            try
            {
                await BeatAsync();
            }
            catch (Exception e) when (e is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // A failed round must not stop the host; the next tick tries again.
                logger.LogWarning(e, "The presence heartbeat failed; trying again next round.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try
        {
            var database = await redis.DatabaseAsync();
            await database.KeyDeleteAsync(redis.AliveKey(redis.InstanceId));
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "Could not delete the presence heartbeat; the other instances clean up once it expires.");
        }
    }

    public async Task BeatAsync()
    {
        var database = await redis.DatabaseAsync();
        // Alive before listed, so no instance ever sees this one listed without a heartbeat.
        await database.StringSetAsync(redis.AliveKey(redis.InstanceId), "1", timing.Ttl);
        await database.SetAddAsync(redis.InstancesKey, redis.InstanceId);
        foreach (var instance in await database.SetMembersAsync(redis.InstancesKey))
        {
            var instanceId = instance.ToString();
            if (instanceId != redis.InstanceId && !await database.KeyExistsAsync(redis.AliveKey(instanceId)))
            {
                await presence.PurgeInstanceAsync(instanceId);
                await database.SetRemoveAsync(redis.InstancesKey, instanceId);
            }
        }
    }
}
