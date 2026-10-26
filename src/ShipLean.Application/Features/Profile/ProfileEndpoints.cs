using Agoda.IoC.Core;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Application.Uploads;
using ShipLean.Contracts;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Profile;
using ShipLean.Contracts.Users;
using ShipLean.Core.People;

namespace ShipLean.Application.Features.Profile;

// The caller's own identity: display name, avatar colour and photo. Every action acts on ICurrentUser, never on an id from
// the request, so there is nobody else's profile to reach. Each answers the caller's MeDto.
[RegisterPerRequest]
public sealed class ProfileEndpoints(NpgsqlDataSource dataSource, ICurrentUser currentUser, IBlobStore blobs, MeReader me, TimeProvider clock, ProxySignInSettings proxy)
{
    public const long MaxPhotoBytes = 2L * 1024 * 1024;

    public async Task<MeDto> UpdateAsync(UpdateProfileRequest request, CancellationToken cancellationToken)
    {
        var name = DisplayName.Problem(request.Name) is { } problem ? throw ApiException.Validation(problem) : request.Name.Trim();
        var colour = request.AvatarColour?.Trim().ToLowerInvariant() ?? string.Empty;
        if (!AvatarPalette.All.Contains(colour))
        {
            throw ApiException.Validation("Pick one of the avatar colours.");
        }

        if (proxy.IsProxy)
        {
            // The name belongs to the identity provider; the colour is still the user's.
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var stored = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
                "SELECT name FROM app_user WHERE id = @id", new { id = currentUser.Id }, cancellationToken: cancellationToken));
            if (stored is not null && stored != name)
            {
                throw ApiException.Forbidden("Your name comes from your sign-in provider.");
            }
        }

        await UpdateRowAsync(
            "UPDATE app_user SET name = @name, initials = @initials, avatar_colour = @colour, updated_at = @now WHERE id = @id",
            new { name, initials = Initials.From(name), colour }, cancellationToken);
        return await me.GetAsync(currentUser.Id, cancellationToken);
    }

    // The caller is known before the body is read; then the file part is streamed with its size enforced while reading
    // (nothing is bound, buffered or spooled for a caller who is refused). The bytes, not the declared type, decide what it is.
    public async Task<MeDto> ReplacePhotoAsync(HttpRequest request, CancellationToken cancellationToken)
    {
        var userId = currentUser.Id;
        var boundary = MultipartUpload.BoundaryOf(request) ?? throw ApiException.Validation("Choose a photo to upload.");
        MultipartUpload.CapBody(request, MaxPhotoBytes);
        var (declared, bytes) = await ReadFilePartAsync(boundary, request.Body, cancellationToken);
        if (!PhotoFormat.DeclarableTypes.Contains(declared))
        {
            throw ApiException.Validation("A photo must be a PNG, JPEG or WebP image.");
        }

        var type = PhotoFormat.Sniff(bytes) ?? throw ApiException.Validation("A photo must be a PNG, JPEG or WebP image.");
        var (width, height) = PhotoFormat.Dimensions(type, bytes) ?? throw ApiException.Validation("That photo is damaged or cut short.");
        if (width is < 1 or > PhotoFormat.MaxSide || height is < 1 or > PhotoFormat.MaxSide)
        {
            throw ApiException.Validation($"A photo can be at most {PhotoFormat.MaxSide} pixels on a side.");
        }

        var key = $"avatars/{userId:N}/{Guid.NewGuid():N}";
        await using (var content = new MemoryStream(bytes))
        {
            await blobs.PutAsync(key, content, type, cancellationToken);
        }

        await UpdateRowAsync(
            "UPDATE app_user SET avatar_photo_blob = @key, photo_version = photo_version + 1, updated_at = @now WHERE id = @id",
            new { key }, cancellationToken);
        return await me.GetAsync(userId, cancellationToken);
    }

    // Clears the reference and bumps the version; the stored bytes are kept.
    public async Task<MeDto> RemovePhotoAsync(CancellationToken cancellationToken)
    {
        await UpdateRowAsync(
            "UPDATE app_user SET avatar_photo_blob = NULL, photo_version = photo_version + 1, updated_at = @now WHERE id = @id AND avatar_photo_blob IS NOT NULL",
            new { }, cancellationToken);
        return await me.GetAsync(currentUser.Id, cancellationToken);
    }

    // Any signed-in user may read any user's photo (it is what a card or the presence stack shows). The blob's content type
    // is the one the upload's bytes proved; versionIsCurrent says whether v (the ?v= the address carries, whatever it holds)
    // is the photo's current version. NotFound for an unknown user, a user without a photo, or a missing blob.
    public async Task<(BlobContent Content, bool VersionIsCurrent)> OpenPhotoAsync(Guid userId, string? v, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<(string? Blob, int Version)>(new CommandDefinition(
            "SELECT avatar_photo_blob AS blob, photo_version AS version FROM app_user WHERE id = @userId", new { userId }, cancellationToken: cancellationToken));
        var content = row.Blob is null ? null : await blobs.GetAsync(row.Blob, cancellationToken);
        return content is null
            ? throw ApiException.NotFound("That user has no photo.")
            : (content, v == row.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private async Task UpdateRowAsync(string sql, object values, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var parameters = new DynamicParameters(values);
        parameters.Add("id", currentUser.Id);
        parameters.Add("now", clock.GetUtcNow());
        await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken));
    }

    // The part named "file" that carries a file name, read whole but never past the limit.
    private static async Task<(string DeclaredType, byte[] Bytes)> ReadFilePartAsync(string boundary, Stream body, CancellationToken cancellationToken)
    {
        var section = await MultipartUpload.NextFileSectionAsync(new MultipartReader(boundary, body), cancellationToken)
            ?? throw ApiException.Validation("Choose a photo to upload.");
        var bytes = await MultipartUpload.ReadAllAsync(section.Body, MaxPhotoBytes, ApiException.Validation("A photo can be at most 2 MB."), cancellationToken);
        var declared = MediaTypeHeaderValue.TryParse(section.ContentType, out var type) ? type.MediaType.Value ?? string.Empty : string.Empty;
        return (declared.ToLowerInvariant(), bytes);
    }
}
