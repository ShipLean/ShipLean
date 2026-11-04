using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Upload;

public interface IDemoDelivery
{
    Task<DemoUploadedDto> DeliverAsync(DemoDeliveryParts parts, CancellationToken cancellationToken = default);
}
