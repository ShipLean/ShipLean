using ShipLean.Application.Features.Demos.Upload;
using ShipLean.Contracts;
using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// The request-scoped face of RecordingDemoDelivery: notes who ICurrentUser was when the delivery was called, which is how a test sees the principal an
// anonymous upload adopted from its token.
internal sealed class ActingDemoDelivery(ICurrentUser currentUser, RecordingDemoDelivery recorder) : IDemoDelivery
{
    public Task<DemoUploadedDto> DeliverAsync(DemoDeliveryParts parts, CancellationToken cancellationToken = default)
    {
        recorder.ActingUserId = currentUser.Id;
        return recorder.DeliverAsync(parts, cancellationToken);
    }
}
