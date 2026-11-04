using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Upload;

public sealed class DemoDelivery : IDemoDelivery
{
    // TODO: the real delivery replaces this: stage the three files, commit them with the sprint's pointers in one transaction.
    public Task<DemoUploadedDto> DeliverAsync(DemoDeliveryParts parts, CancellationToken cancellationToken = default)
        => throw new NotSupportedException();
}
