using System.Security.Claims;
using System.Text;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Npgsql;
using ShipLean.Application.Markdown;
using ShipLean.Application.Uploads;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Upload;

// K8's protocol: POST api/uploads/demo?token= with chapters, script and video in that order. Any token that is not live, a member who is no longer one
// and a delivery that lost to a retry all answer the same 404. The token is claimed after the membership check and before a byte of the body is read,
// and given back on any failure so the URL can be retried until it expires. The token is never logged or put in an error: it is only ever an argument.
public sealed class DemoUploads(ITransferTokens tokens, IProjectAccess access, IDemoDelivery delivery, NpgsqlDataSource dataSource) : IDemoUploads
{
    private const string ScriptPart = "script";
    private const string VideoPart = "video";
    private const int MaxScriptBytes = MarkdownRenderer.MaxLength * 4;
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    public async Task<DemoUploadedDto> UploadAsync(string token, HttpRequest request, CancellationToken cancellationToken = default)
    {
        var grant = await tokens.FindAsync(TransferKind.Demo, token, cancellationToken) ?? throw NotValid();
        // The request acts as the token's user from here on (ICurrentUser reads the principal).
        request.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, grant.UserId.ToString())], "demo-token"));
        await RequireMemberAsync(grant, cancellationToken);
        if (!await tokens.ClaimAsync(token, cancellationToken))
        {
            throw NotValid();
        }

        try
        {
            return await ReadAndDeliverAsync(grant, token, request, cancellationToken);
        }
        catch
        {
            await tokens.ReleaseAsync(token, CancellationToken.None);
            throw;
        }
    }

    private static ApiException NotValid() => ApiException.NotFound("That upload link is not valid.");

    private async Task RequireMemberAsync(TransferGrant grant, CancellationToken cancellationToken)
    {
        try
        {
            await access.RequireAsync(ProjectItem.WorkItem, grant.WorkItemId, ProjectRole.Member, cancellationToken);
        }
        catch (ApiException refusal) when (refusal.Code == ApiErrorCodes.NotFound)
        {
            throw NotValid();
        }
    }

    private async Task<DemoUploadedDto> ReadAndDeliverAsync(TransferGrant grant, string token, HttpRequest request, CancellationToken cancellationToken)
    {
        var boundary = MultipartUpload.BoundaryOf(request) ?? throw ApiException.Validation("Send chapters, script and video as multipart/form-data.");
        // The delivery counts the video's bytes against its own limit, so Kestrel's cap (30 MB by default) is lifted, as for any upload.
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } cap)
        {
            cap.MaxRequestBodySize = null;
        }

        var reader = new MultipartReader(boundary, request.Body);
        var chapters = DemoChaptersFile.Parse(await ReadPartAsync(reader, DemoChaptersFile.Part, DemoChaptersFile.MaxBytes, cancellationToken));
        await RequireFeaturesAsync(grant.WorkItemId, chapters, cancellationToken);
        var script = ScriptOf(await ReadPartAsync(reader, ScriptPart, MaxScriptBytes, cancellationToken));
        var section = await NextSectionAsync(reader, VideoPart, cancellationToken);
        var video = await Mp4Sniff.CheckedAsync(section.Body, cancellationToken);
        try
        {
            return await delivery.DeliverAsync(new DemoDeliveryParts(grant, token, chapters, script, video), cancellationToken);
        }
        catch (ApiException superseded) when (superseded.Code == ApiErrorCodes.NotFound)
        {
            throw NotValid();
        }
    }

    private static async Task<byte[]> ReadPartAsync(MultipartReader reader, string name, int limit, CancellationToken cancellationToken)
    {
        var section = await NextSectionAsync(reader, name, cancellationToken);
        return await MultipartUpload.ReadAllAsync(section.Body, limit, ApiException.Validation($"The {name} part is too large.", name), cancellationToken);
    }

    // The next part, which must be the one named. A missing part and a part out of order are both named in the answer.
    private static async Task<MultipartSection> NextSectionAsync(MultipartReader reader, string name, CancellationToken cancellationToken)
    {
        MultipartSection? section;
        try
        {
            section = await reader.ReadNextSectionAsync(cancellationToken);
        }
        catch (InvalidDataException)
        {
            throw MultipartUpload.Unreadable();
        }
        catch (IOException error) when (MultipartUpload.IsTruncation(error))
        {
            throw MultipartUpload.Unreadable();
        }

        if (section is null)
        {
            throw ApiException.Validation($"The {name} part is missing: send chapters, script and video, in that order.", name);
        }

        var found = ContentDispositionHeaderValue.TryParse(section.ContentDisposition, out var disposition)
            ? HeaderUtilities.RemoveQuotes(disposition.Name).Value
            : null;
        return found == name
            ? section
            : throw ApiException.Validation($"Expected the {name} part next but found {found ?? "an unnamed part"}: send chapters, script and video, in that order.", name);
    }

    private static string ScriptOf(byte[] bytes)
    {
        string script;
        try
        {
            script = StrictUtf8.GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw ApiException.Validation("The script must be UTF-8 text.", ScriptPart);
        }

        if (string.IsNullOrWhiteSpace(script))
        {
            throw ApiException.Validation("The script is blank.", ScriptPart);
        }

        return script.Length > MarkdownRenderer.MaxLength
            ? throw ApiException.Validation($"The script is too long: {script.Length:N0} characters, the limit is {MarkdownRenderer.MaxLength:N0}.", ScriptPart)
            : script;
    }

    private async Task RequireFeaturesAsync(Guid workItemId, DemoChaptersJson chapters, CancellationToken cancellationToken)
    {
        var keys = chapters.Chapters.Where(c => c.FeatureKey is not null).Select(c => c.FeatureKey!.Trim().ToUpperInvariant()).Distinct().ToArray();
        if (keys.Length == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var live = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT w.key FROM work_item w JOIN state s ON s.id = w.state_id JOIN work_item demo ON demo.project_id = w.project_id WHERE demo.id = @workItemId AND w.key = ANY(@keys) AND s.category <> 'removed'",
            new { workItemId, keys }, cancellationToken: cancellationToken))).ToHashSet();
        if (keys.FirstOrDefault(key => !live.Contains(key)) is { } missing)
        {
            throw ApiException.Validation($"The chapters name {missing}, which is not an item of this project.", DemoChaptersFile.Part);
        }
    }
}
