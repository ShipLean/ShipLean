using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using ShipLean.Application.Uploads;
using ShipLean.Contracts;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Brand;

// PUT .../demo-brand/logo. The caller is known before the body is read; the file part is streamed with its size enforced while reading. The bytes
// decide what the logo is (DemoLogoFile); the key is one we generate, never the uploaded name. Every refusal is 400, field "logo".
public sealed class DemoLogoUpload(IDemoLogoStore store, IBlobStore blobs, IDemoBrand brand, ICurrentUser currentUser) : IDemoLogoUpload
{
    private const string Field = "logo";

    public async Task<ProjectDemoBrandDto> ReplaceAsync(Guid projectId, HttpRequest request, CancellationToken cancellationToken = default)
    {
        var userId = currentUser.Id;
        var boundary = MultipartUpload.BoundaryOf(request) ?? throw Refused("Choose a logo to upload.");
        MultipartUpload.CapBody(request, DemoLogoFile.MaxBytes);
        var section = await MultipartUpload.NextFileSectionAsync(new MultipartReader(boundary, request.Body), cancellationToken)
            ?? throw Refused("Choose a logo to upload.");
        var bytes = await MultipartUpload.ReadAllAsync(section.Body, DemoLogoFile.MaxBytes, Refused("A logo can be at most 1 MB."), cancellationToken);

        var decided = DemoLogoFile.Decide(bytes, out var refusal) ?? throw Refused(refusal ?? DemoLogoFile.NotAnImage);

        var key = $"demo-logos/{projectId:N}/{Guid.NewGuid():N}";
        await using (var content = new MemoryStream(decided.Bytes))
        {
            await blobs.PutAsync(key, content, decided.ContentType, cancellationToken);
        }

        await store.SetAsync(projectId, key, userId, cancellationToken);
        return await brand.ReadAsync(projectId, cancellationToken);
    }

    private static ApiException Refused(string message) => ApiException.Validation(message, Field);
}
