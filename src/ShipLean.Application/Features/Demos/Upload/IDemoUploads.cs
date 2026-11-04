using Microsoft.AspNetCore.Http;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Upload;

// POST api/uploads/demo?token=: takes the request, as AttachmentUploads does, so a test can replace it.
public interface IDemoUploads
{
    Task<DemoUploadedDto> UploadAsync(string token, HttpRequest request, CancellationToken cancellationToken = default);
}
