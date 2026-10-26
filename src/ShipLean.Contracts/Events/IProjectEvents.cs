namespace ShipLean.Contracts.Events;

// The in-process event bus. Publishers call PublishAsync and move on: delivery to subscribers happens off the
// publishing request, in publish order per subscriber, so a slow handler never blocks a save. Publishers call it
// after their transaction commits, never inside it: a subscriber that refetches must see the change.
// Subscribe(projectId, handler) delivers one project's events (null = every project) until the returned IDisposable is
// disposed; a handler that throws is logged and the subscription carries on.
public interface IProjectEvents
{
    Task PublishAsync(ProjectEvent projectEvent);

    IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler);
}
