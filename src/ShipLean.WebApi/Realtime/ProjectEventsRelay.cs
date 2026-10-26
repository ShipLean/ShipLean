using System.Text.Json;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Realtime;

// Subscribes once to the in-process bus for every project and forwards each event to the clients that joined that
// project. The bus delivers off the publishing request and in publish order, so a slow client never blocks a save.
// The event is serialised as the base type with the API's own options (camelCase string enums): by runtime type the
// "type" discriminator would be dropped.
public sealed class ProjectEventsRelay(IProjectEvents events, IHubContext<ProjectHub> hub, IOptions<JsonOptions> json) : IHostedService
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = events.Subscribe(null, ForwardAsync);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }

    private Task ForwardAsync(ProjectEvent projectEvent)
        => hub.Clients.Group(ProjectHub.GroupName(projectEvent.ProjectId))
            .SendAsync(ProjectHub.EventMethod, JsonSerializer.SerializeToElement(projectEvent, json.Value.SerializerOptions));
}
