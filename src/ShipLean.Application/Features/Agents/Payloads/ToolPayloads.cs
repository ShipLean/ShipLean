using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Agents.Stores;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Agents.Payloads;

// tool_payload: the payload id is 16 random bytes and the upload token 32, both base64url; only the token's SHA-256 is stored.
// An upload is validated in memory and then stored by one UPDATE that also checks the token is still live, so of two
// concurrent PUTs only one stores, and a refused or interrupted upload leaves the link usable until upload_by.
// A token or payload dies when the agent authorization that asked for it is revoked, or when its user is disabled or their
// security stamp no longer matches the authorization's (the rules TransferTokens applies). Expired bodies are cleared on the next issue.
public sealed class ToolPayloads(NpgsqlDataSource dataSource, TimeProvider clock) : IToolPayloads
{
    private const string NotLive = "That upload link is not valid.";

    private static readonly UTF8Encoding StrictUtf8 = new(false, true);
    private static readonly byte[] Bom = [0xEF, 0xBB, 0xBF];

    private sealed record Pending(string Tool, string? Field);

    public async Task<IssuedPayload> IssueAsync(NewToolPayload payload, CancellationToken cancellationToken = default)
    {
        var id = StoreSecrets.Base64Url(RandomNumberGenerator.GetBytes(16));
        var token = StoreSecrets.New();
        var now = clock.GetUtcNow();
        var uploadBy = now.Add(IToolPayloads.UploadWindow);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE tool_payload SET body = NULL WHERE expires_at < @now AND body IS NOT NULL;
            INSERT INTO tool_payload (id, token_hash, tool, field, user_id, authorization_id, agent_stamp, created_at, upload_by)
            VALUES (@id, @hash, @tool, @field, @userId, @authorizationId, @agentStamp, @now, @uploadBy);
            """,
            new { id, hash = StoreSecrets.Hash(token), payload.Tool, payload.Field, payload.UserId, payload.AuthorizationId, payload.AgentStamp, now, uploadBy },
            cancellationToken: cancellationToken));
        return new IssuedPayload(id, token, uploadBy);
    }

    public async Task<UploadedPayloadDto> UploadAsync(string token, Stream body, CancellationToken cancellationToken = default)
    {
        var hash = StoreSecrets.Hash(token);
        Pending pending;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            pending = await connection.QuerySingleOrDefaultAsync<Pending>(new CommandDefinition(
                """
                SELECT p.tool, p.field FROM tool_payload p
                JOIN app_user u ON u.id = p.user_id AND u.disabled_at IS NULL
                WHERE p.token_hash = @hash AND p.uploaded_at IS NULL AND p.upload_by > @now AND (p.authorization_id IS NULL OR EXISTS (SELECT 1 FROM oauth_authorization a WHERE a.id = p.authorization_id AND a.revoked_at IS NULL AND a.security_stamp = u.security_stamp))
                """,
                new { hash, now = clock.GetUtcNow() }, cancellationToken: cancellationToken))
                ?? throw ApiException.NotFound(NotLive);
        }

        var bytes = Validate(await ReadAsync(body, cancellationToken), pending.Field);

        var now = clock.GetUtcNow();
        await using var store = await dataSource.OpenConnectionAsync(cancellationToken);
        return await store.QuerySingleOrDefaultAsync<UploadedPayloadDto>(new CommandDefinition(
            """
            UPDATE tool_payload p SET uploaded_at = @now, expires_at = @expiresAt, size_bytes = @size, body = @bytes
            FROM app_user u
            WHERE u.id = p.user_id AND u.disabled_at IS NULL
              AND p.token_hash = @hash AND p.uploaded_at IS NULL AND p.upload_by > @now AND (p.authorization_id IS NULL OR EXISTS (SELECT 1 FROM oauth_authorization a WHERE a.id = p.authorization_id AND a.revoked_at IS NULL AND a.security_stamp = u.security_stamp))
            RETURNING p.tool, p.field, p.size_bytes AS sizeBytes, p.expires_at AS expiresAt
            """,
            new { hash, now, expiresAt = now.Add(IToolPayloads.Lifetime), size = bytes.Length, bytes }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound(NotLive);
    }

    public async Task<ToolPayload?> FindAsync(string payloadId, string tool, Guid userId, Guid? authorizationId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<ToolPayload>(new CommandDefinition(
            """
            SELECT p.tool, p.field, p.body FROM tool_payload p
            JOIN app_user u ON u.id = p.user_id AND u.disabled_at IS NULL
            WHERE p.id = @payloadId AND p.tool = @tool AND p.user_id = @userId AND p.authorization_id IS NOT DISTINCT FROM @authorizationId::uuid
              AND p.uploaded_at IS NOT NULL AND p.expires_at > @now AND p.body IS NOT NULL AND (p.authorization_id IS NULL OR EXISTS (SELECT 1 FROM oauth_authorization a WHERE a.id = p.authorization_id AND a.revoked_at IS NULL AND a.security_stamp = u.security_stamp))
            """,
            new { payloadId, tool, userId, authorizationId, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
    }

    // Asks each read for at most the bytes left before MaxBytes plus one, so it never takes more than MaxBytes + 1 bytes off
    // the request and refuses at the first byte past MaxBytes.
    private static async Task<byte[]> ReadAsync(Stream body, CancellationToken cancellationToken)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[81920];
        int read;
        while ((read = await body.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, IToolPayloads.MaxBytes + 1 - memory.Length)), cancellationToken)) > 0)
        {
            if (memory.Length + read > IToolPayloads.MaxBytes)
            {
                throw ApiException.Validation($"The upload is too large: a payload can be at most {IToolPayloads.MaxBytes / (1024 * 1024)} MB. Send the arguments inline, or split the work into smaller calls.");
            }
            await memory.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
        return memory.ToArray();
    }

    // Strict UTF-8 with a leading BOM stripped; with no field, exactly one JSON object.
    private static byte[] Validate(byte[] bytes, string? field)
    {
        if (bytes.AsSpan().StartsWith(Bom))
        {
            bytes = bytes[Bom.Length..];
        }
        if (bytes.Length == 0)
        {
            throw ApiException.Validation("The upload is empty. Send the file with the content in it.");
        }
        try
        {
            _ = StrictUtf8.GetCharCount(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw ApiException.Validation("The upload is not valid UTF-8 text. Save the file as UTF-8 and send it again to the same URL.");
        }
        if (field is null && !IsOneJsonObject(bytes))
        {
            throw ApiException.Validation(
                "Without field, the upload must be one JSON object of the tool's arguments, such as {\"project\":\"SHIP\"}. Fix the file and send it again to the same URL, or ask for an upload with field to send one argument as raw text.");
        }
        return bytes;
    }

    private static bool IsOneJsonObject(byte[] bytes)
    {
        try
        {
            using var document = JsonDocument.Parse(bytes);
            return document.RootElement.ValueKind == JsonValueKind.Object;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
