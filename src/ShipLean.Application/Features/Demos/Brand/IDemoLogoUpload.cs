using Microsoft.AspNetCore.Http;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Brand;

public interface IDemoLogoUpload
{
    Task<ProjectDemoBrandDto> ReplaceAsync(Guid projectId, HttpRequest request, CancellationToken cancellationToken = default);
}
