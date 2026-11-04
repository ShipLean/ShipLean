using Microsoft.Extensions.Hosting;
using ShipLean.Application.Storage;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// Takes AttachmentLimits at startup, so a bad SHIPLEAN_ATTACHMENT_* or SHIPLEAN_DEMO_MAX_BYTES setting fails host start naming it.
public sealed class AttachmentLimitsStartup(AttachmentLimits limits) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = limits;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
