using ShipLean.WebApi.Realtime;

namespace ShipLean.WebApi.Tests.Realtime;

[TestFixture]
public class ProjectGroupMembersTests
{
    [Test]
    public async Task Disconnected_WhenConnectionClosed_ShouldNotBeRemovedFromTheGroupLater()
    {
        var groups = new RecordingGroups();
        var members = new ProjectGroupMembers(new FakeHubContext(groups), new PresenceTracker(new NullBus()));
        var project = Guid.NewGuid();
        var user = Guid.NewGuid();
        members.Joined("closed", user, project);
        members.Joined("open", user, project);

        members.Disconnected("closed");
        await members.RemoveUserAsync(project, user);

        groups.Removed.ShouldBe(["open"]);
    }
}
