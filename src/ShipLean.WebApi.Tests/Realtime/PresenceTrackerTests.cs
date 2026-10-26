using ShipLean.Contracts.Events;
using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Tests.Realtime;

// The tracker alone, on a bus that records what is published and in what order.
[TestFixture]
public class PresenceTrackerTests
{
    private sealed class RecordingBus : IProjectEvents
    {
        private readonly List<ProjectEvent> _published = [];

        public async Task PublishAsync(ProjectEvent projectEvent)
        {
            // Lets a competing publisher in, as a real queue under load would.
            await Task.Yield();
            lock (_published)
            {
                _published.Add(projectEvent);
            }
        }

        public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => throw new NotSupportedException();

        public IReadOnlyList<PresenceChanged> Presence(string screenId)
        {
            lock (_published)
            {
                return _published.OfType<PresenceChanged>().Where(p => p.ScreenId == screenId).ToList();
            }
        }
    }

    [Test]
    public async Task Presence_WhenSixteenJoinAtOnce_ShouldPublishFullListLast()
    {
        var project = Guid.NewGuid();
        for (var round = 0; round < 50; round++)
        {
            var bus = new RecordingBus();
            var tracker = new PresenceTracker(bus);
            var users = Enumerable.Range(0, 16).Select(_ => Guid.NewGuid()).ToList();

            await Task.WhenAll(users.Select((user, i) => Task.Run(() => tracker.JoinAsync($"c{i}", user, project, "backlog"))));

            bus.Presence("backlog")[^1].UserIds.Order().ShouldBe(users.Order());
        }
    }

    [Test]
    public async Task Presence_WhenSixteenJoinAndLeaveAtOnce_ShouldPublishTheTruthLast()
    {
        var project = Guid.NewGuid();
        for (var round = 0; round < 50; round++)
        {
            var bus = new RecordingBus();
            var tracker = new PresenceTracker(bus);
            var users = Enumerable.Range(0, 16).Select(_ => Guid.NewGuid()).ToList();
            await Task.WhenAll(users.Select((user, i) => tracker.JoinAsync($"c{i}", user, project, "board")));

            await Task.WhenAll(users.Take(8).Select((_, i) => Task.Run(() => tracker.DisconnectedAsync($"c{i}"))));

            bus.Presence("board")[^1].UserIds.Order().ShouldBe(users.Skip(8).Order());
        }
    }

    [Test]
    public async Task Presence_WhenScreensChange_ShouldNumberEveryListAboveTheOnesBefore()
    {
        var bus = new RecordingBus();
        var tracker = new PresenceTracker(bus);
        var project = Guid.NewGuid();

        await tracker.JoinAsync("c1", Guid.NewGuid(), project, "board");
        await tracker.JoinAsync("c2", Guid.NewGuid(), project, "backlog");
        await tracker.JoinAsync("c1", Guid.NewGuid(), project, "backlog");
        await tracker.DisconnectedAsync("c2");
        await tracker.JoinAsync("c3", Guid.NewGuid(), project, "board");

        var sequences = bus.Presence("board").Concat(bus.Presence("backlog")).Select(p => p.Sequence).Order().ToList();
        sequences.ShouldBe(Enumerable.Range(1, 6).Select(i => (long)i).ToList());
        bus.Presence("board").Select(p => p.Sequence).ShouldBeInOrder(SortDirection.Ascending);
    }
}
