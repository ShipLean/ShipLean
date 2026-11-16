using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.AgentDispatch.RunLogs;

// The event bus, except that publishing attachmentsChanged throws: the step AttachmentStore runs after its transaction commits.
internal sealed class ThrowingAttachmentEvents(IProjectEvents inner) : IProjectEvents, IDisposable
{
    public Task PublishAsync(ProjectEvent projectEvent)
        => projectEvent is AttachmentsChanged ? throw new InvalidOperationException("The event bus is down.") : inner.PublishAsync(projectEvent);

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => inner.Subscribe(projectId, handler);

    public void Dispose() => (inner as IDisposable)?.Dispose();
}
