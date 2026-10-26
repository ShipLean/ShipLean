using Microsoft.AspNetCore.SignalR;

namespace ShipLean.WebApi.Tests.Realtime;

// Group changes, recorded instead of made.
internal sealed class RecordingGroups : IGroupManager
{
    internal List<string> Removed { get; } = [];

    public Task AddToGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task RemoveFromGroupAsync(string connectionId, string groupName, CancellationToken cancellationToken = default)
    {
        Removed.Add(connectionId);
        return Task.CompletedTask;
    }
}
