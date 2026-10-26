using System.Security.Claims;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Time.Testing;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Tests.Realtime;

// The sweep must outlive a database error (an exception out of a BackgroundService stops the host), and it must be
// scheduled in the real host.
[TestFixture]
[CancelAfter(30000)]
public class HubSessionSweepTests
{
    private static readonly Guid Healthy = Guid.NewGuid();
    private static readonly Guid Ended = Guid.NewGuid();
    private static readonly Guid Broken = Guid.NewGuid();

    [Test]
    public async Task Sweep_WhenOneSessionReadThrows_ShouldReturnAndStillCheckTheOthers()
    {
        var (sweep, connections) = Arrange(new FakeTimeProvider());
        var broken = Open(connections, Broken);
        var ended = Open(connections, Ended);
        var healthy = Open(connections, Healthy);

        await sweep.SweepAsync(CancellationToken.None);

        broken.Aborted.ShouldBeFalse();
        healthy.Aborted.ShouldBeFalse();
        ended.Aborted.ShouldBeTrue();
    }

    [Test]
    public async Task Sweep_WhenEveryReadThrows_ShouldRunTheNextRound()
    {
        var clock = new FakeTimeProvider();
        var sessions = new StubSessions();
        var (sweep, connections) = Arrange(clock, sessions);
        Open(connections, Broken);
        var ended = Open(connections, Ended);
        sessions.ThrowForAll = true;
        await sweep.StartAsync(CancellationToken.None);

        // Round one: every read throws; the loop must still be alive for round two.
        await AdvanceUntilAsync(clock, sessions.ReadsAtLeastAsync(2));
        sessions.ThrowForAll = false;
        await AdvanceUntilAsync(clock, ended.AbortedAsync());

        ended.Aborted.ShouldBeTrue();
        await sweep.StopAsync(CancellationToken.None);
    }

    // The sweep creates its timer on its own thread, so a tick may precede it: keep ticking until the awaited thing happens.
    private static async Task AdvanceUntilAsync(FakeTimeProvider clock, Task done)
    {
        while (!done.IsCompleted)
        {
            clock.Advance(HubSessionSweep.Period);
            await Task.Yield();
        }
        await done;
    }

    private static (HubSessionSweep Sweep, HubConnections Connections) Arrange(TimeProvider clock, StubSessions? sessions = null)
    {
        var connections = new HubConnections();
        var check = new HubSessionCheck(sessions ?? new StubSessions());
        // No connection joins a project here, so the membership half of the sweep has nothing to read.
        var members = new ProjectGroupMembers(new FakeHubContext(new RecordingGroups()), new PresenceTracker(new NullBus()));
        var membership = new HubMembership(NpgsqlDataSource.Create("Host=localhost"));
        return (new HubSessionSweep(connections, check, members, membership, clock, NullLogger<HubSessionSweep>.Instance), connections);
    }

    private static FakeContext Open(HubConnections connections, Guid userId)
    {
        var context = new FakeContext(userId);
        connections.Opened(context);
        return context;
    }

    // Completes by the test, or is cancelled after ten seconds so a missed signal fails instead of hanging.
    private static TaskCompletionSource TimedOut()
    {
        var source = new TaskCompletionSource();
        new CancellationTokenSource(TimeSpan.FromSeconds(10)).Token.Register(() => source.TrySetCanceled());
        return source;
    }

    public sealed class StubSessions : UserSessions
    {
        private int _reads;
        private readonly TaskCompletionSource _enough = TimedOut();
        private int _wanted;

        public StubSessions() : base(NpgsqlDataSource.Create("Host=localhost"), TimeProvider.System)
        {
        }

        public bool ThrowForAll { get; set; }

        public Task ReadsAtLeastAsync(int count)
        {
            _wanted = count;
            return Volatile.Read(ref _reads) >= count ? Task.CompletedTask : _enough.Task;
        }

        protected override Task<UserSnapshot?> QueryAsync(Guid userId, CancellationToken cancellationToken)
        {
            if (Interlocked.Increment(ref _reads) >= _wanted && _wanted > 0)
            {
                _enough.TrySetResult();
            }
            if (ThrowForAll || userId == Broken)
            {
                throw new NpgsqlException("the database went away");
            }
            return Task.FromResult<UserSnapshot?>(new UserSnapshot(userId, false, false, userId == Ended, "stamp"));
        }
    }

    private sealed class FakeContext : HubCallerContext
    {
        private readonly TaskCompletionSource _aborted = TimedOut();

        public FakeContext(Guid userId)
        {
            ConnectionId = Guid.NewGuid().ToString();
            User = new ClaimsPrincipal(new ClaimsIdentity(
                [new Claim(ClaimTypes.NameIdentifier, userId.ToString()), new Claim(AuthPrincipal.StampClaim, "stamp")], "test"));
        }

        public bool Aborted => _aborted.Task.IsCompleted;

        public Task AbortedAsync() => _aborted.Task;

        public override string ConnectionId { get; }
        public override string? UserIdentifier => null;
        public override ClaimsPrincipal? User { get; }
        public override IDictionary<object, object?> Items { get; } = new Dictionary<object, object?>();
        public override IFeatureCollection Features { get; } = new FeatureCollection();
        public override CancellationToken ConnectionAborted => CancellationToken.None;

        public override void Abort() => _aborted.TrySetResult();
    }
}

// The real host schedules the sweep: removing its hosted-service registration leaves it never running.
[TestFixture]
public class HubSessionSweepScheduleTests : ApiFixture
{
    [Test]
    public void Host_ShouldRunTheSessionSweepAsAHostedService()
    {
        Host.Services.GetServices<IHostedService>().OfType<HubSessionSweep>().ShouldNotBeEmpty();
    }
}
