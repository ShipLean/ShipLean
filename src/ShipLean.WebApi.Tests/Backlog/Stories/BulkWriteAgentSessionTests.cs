using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.AgentDispatch;
using static ShipLean.WebApi.Tests.Backlog.Stories.BulkWriteWire;

namespace ShipLean.WebApi.Tests.Backlog.Stories;

// A bulk write of one item is an edit, so a state transition with a Send to agent action opens a session; a bulk write of
// several items never does. The host's own subscriber is replaced by a probe running the same AgentTrigger, as AgentTriggerTests does.
[TestFixture]
public class BulkWriteAgentSessionTests : ApiFixture
{
    private readonly Channel<(ProjectEvent Event, Exception? Failure)> _handled = Channel.CreateUnbounded<(ProjectEvent, Exception?)>();
    private IDisposable _probe = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.Remove(services.Single(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(AgentDispatchSubscriber)));

    [OneTimeSetUp]
    public void Probe()
    {
        var trigger = Host.Services.GetRequiredService<AgentTrigger>();
        _probe = Host.Services.GetRequiredService<IProjectEvents>().Subscribe(null, async projectEvent =>
        {
            Exception? failure = null;
            try
            {
                await trigger.HandleAsync(projectEvent, CancellationToken.None);
            }
            catch (Exception failed)
            {
                failure = failed;
            }

            _handled.Writer.TryWrite((projectEvent, failure));
        });
    }

    [OneTimeTearDown]
    public void StopProbe() => _probe.Dispose();

    [Test]
    public async Task Bulk_WhenOneItem_ShouldStartAgentSessionLikeAnEdit()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        await AgentDispatchArrange.AddTransitionAsync(database, arranged.Project.Id, WorkItemTypeId.Story, arranged.Project.State(WorkItemTypeId.Story, "New"), arranged.Active);
        var item = await arranged.CreateItemAsync(Api);

        (await BulkAsync(Api, arranged.Project.Id, [Item(item.Id, item.Revision, State(arranged.Active), null)])).ShouldReport([item.Id], []);
        await HandledAsync(item.Id);

        (await AgentDispatchArrange.SessionsAsync(database, item.Id)).ShouldHaveSingleItem().State.ShouldBe("submitted");
    }

    [Test]
    public async Task Bulk_WhenManyItems_ShouldStartNoAgentSession()
    {
        await using var database = await OpenDatabaseAsync();
        var arranged = await AgentDispatchArrange.CreateAsync(database, Api);
        await AgentDispatchArrange.AddTransitionAsync(database, arranged.Project.Id, WorkItemTypeId.Story, arranged.Project.State(WorkItemTypeId.Story, "New"), arranged.Active);
        var first = await arranged.CreateItemAsync(Api);
        var second = await arranged.CreateItemAsync(Api);

        (await BulkAsync(Api, arranged.Project.Id, [Item(first.Id, first.Revision, State(arranged.Active), null), Item(second.Id, second.Revision, State(arranged.Active), null)])).ShouldReport([first.Id, second.Id], []);
        await HandledAsync(second.Id);

        (await AgentDispatchArrange.SessionsAsync(database, first.Id)).ShouldBeEmpty();
        (await AgentDispatchArrange.SessionsAsync(database, second.Id)).ShouldBeEmpty();
    }

    private static System.Text.Json.Nodes.JsonObject State(Guid stateId) => new() { ["state"] = new System.Text.Json.Nodes.JsonObject { ["value"] = stateId } };

    // Waits until the trigger has handled the item's update event: the bus delivers in publish order, so the work is done by then.
    private async Task HandledAsync(Guid workItemId)
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        while (true)
        {
            var (projectEvent, failure) = await _handled.Reader.ReadAsync(timeout.Token);
            if (failure is not null)
            {
                throw failure;
            }

            if (projectEvent is WorkItemUpdated updated && updated.Id == workItemId)
            {
                return;
            }
        }
    }
}
