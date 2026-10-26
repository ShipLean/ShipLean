using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Identity;

namespace ShipLean.UnitTest.Application.Identity;

[TestFixture]
public class UserSessionsTests
{
    private static readonly Guid UserId = Guid.NewGuid();

    [Test]
    public async Task GetAsync_WhenForgottenWhileAReadIsInFlight_ShouldNotCacheTheOldStamp()
    {
        var sessions = new GatedSessions(new FakeTimeProvider());
        sessions.Stamp = "old";
        var slowRead = sessions.GetAsync(UserId);
        await sessions.ReadStarted.Task;

        sessions.Stamp = "new";
        sessions.Forget(UserId);
        sessions.Release.SetResult();
        var stale = await slowRead;
        var next = await sessions.GetAsync(UserId);

        stale!.SecurityStamp.ShouldBe("old");
        next!.SecurityStamp.ShouldBe("new");
    }

    [Test]
    public async Task GetAsync_WhenNothingIsForgotten_ShouldServeTheCachedRead()
    {
        var sessions = new GatedSessions(new FakeTimeProvider());
        sessions.Release.SetResult();
        sessions.Stamp = "first";
        await sessions.GetAsync(UserId);
        sessions.Stamp = "second";

        var cached = await sessions.GetAsync(UserId);

        cached!.SecurityStamp.ShouldBe("first");
    }

    // The database read is held at a gate; the first read signals that it started, then waits to be released.
    private sealed class GatedSessions(TimeProvider clock) : UserSessions(NpgsqlDataSource.Create("Host=unused"), clock)
    {
        private int _reads;

        public string Stamp { get; set; } = string.Empty;

        public TaskCompletionSource ReadStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        protected override async Task<UserSnapshot?> QueryAsync(Guid userId, CancellationToken cancellationToken)
        {
            var stamp = Stamp;
            if (Interlocked.Increment(ref _reads) == 1)
            {
                ReadStarted.SetResult();
                await Release.Task;
            }
            return new UserSnapshot(userId, false, false, false, stamp);
        }
    }
}
