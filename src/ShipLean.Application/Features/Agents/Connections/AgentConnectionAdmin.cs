using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Agents.Connections;

// What an instance admin does to connections (the caller is checked by [InstanceAdmin] on the controller). A signing secret
// goes in encrypted and a callback token as its hash; each comes out once, in the answer to the call that made it.
[RegisterPerRequest]
public sealed class AgentConnectionAdmin(NpgsqlDataSource dataSource, AgentConnectionSecrets secrets, ICurrentUser currentUser, TimeProvider clock)
{
    private const int MaxName = 80;
    private const int MaxUrl = 2000;

    public async Task<AgentConnectionIssuedDto> CreateAsync(AgentConnectionPut body, CancellationToken cancellationToken)
    {
        var (name, url) = Check(body);
        var secret = AgentConnectionSecrets.New();
        var token = AgentConnectionSecrets.New();
        var id = Guid.CreateVersion7();
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireFreeNameAsync(connection, name, null, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO agent_connection (id, type, name, receiver_url, signing_secret_protected, signing_secret_last4, callback_token_hash, callback_token_last4,
                                          created_by, created_at, updated_by, updated_at)
            VALUES (@id, @type, @name, @url, @protected, @secretLast4, @tokenHash, @tokenLast4, @userId, @now, @userId, @now)
            """,
            new
            {
                id, type = AgentConnectionTypes.LinearCompatible, name, url, @protected = secrets.Protect(secret), secretLast4 = AgentConnectionSecrets.LastFour(secret),
                tokenHash = IAgentConnections.HashCallbackToken(token), tokenLast4 = AgentConnectionSecrets.LastFour(token), userId = currentUser.Id, now,
            },
            cancellationToken: cancellationToken));
        return new AgentConnectionIssuedDto(await ReadAsync(connection, id, cancellationToken), secret, token);
    }

    public async Task<AgentConnectionDto> UpdateAsync(Guid id, AgentConnectionPut body, CancellationToken cancellationToken)
    {
        var (name, url) = Check(body);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireLiveAsync(connection, id, cancellationToken);
        await RequireFreeNameAsync(connection, name, id, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE agent_connection SET name = @name, receiver_url = @url, updated_by = @userId, updated_at = @now WHERE id = @id",
            new { id, name, url, userId = currentUser.Id, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
        return await ReadAsync(connection, id, cancellationToken);
    }

    public async Task<AgentConnectionIssuedDto> RegenerateSecretAsync(Guid id, CancellationToken cancellationToken)
    {
        var secret = AgentConnectionSecrets.New();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireLiveAsync(connection, id, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE agent_connection SET signing_secret_protected = @protected, signing_secret_last4 = @last4, updated_by = @userId, updated_at = @now WHERE id = @id",
            new { id, @protected = secrets.Protect(secret), last4 = AgentConnectionSecrets.LastFour(secret), userId = currentUser.Id, now = clock.GetUtcNow() },
            cancellationToken: cancellationToken));
        return new AgentConnectionIssuedDto(await ReadAsync(connection, id, cancellationToken), secret, null);
    }

    public async Task<AgentConnectionIssuedDto> RegenerateTokenAsync(Guid id, CancellationToken cancellationToken)
    {
        var token = AgentConnectionSecrets.New();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireLiveAsync(connection, id, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE agent_connection SET callback_token_hash = @hash, callback_token_last4 = @last4, updated_by = @userId, updated_at = @now WHERE id = @id",
            new { id, hash = IAgentConnections.HashCallbackToken(token), last4 = AgentConnectionSecrets.LastFour(token), userId = currentUser.Id, now = clock.GetUtcNow() },
            cancellationToken: cancellationToken));
        return new AgentConnectionIssuedDto(await ReadAsync(connection, id, cancellationToken), null, token);
    }

    // Archiving is permanent and idempotent. Projects that picked the connection keep the pointer (shown as archived) but can't dispatch.
    public async Task<AgentConnectionDto> ArchiveAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var now = clock.GetUtcNow();
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE agent_connection SET archived_at = @now, updated_by = @userId, updated_at = @now WHERE id = @id AND archived_at IS NULL",
            new { id, userId = currentUser.Id, now }, cancellationToken: cancellationToken));
        return await ReadAsync(connection, id, cancellationToken);
    }

    private static (string Name, string Url) Check(AgentConnectionPut? body)
    {
        var name = body?.Name?.Trim() ?? string.Empty;
        if (name.Length == 0 || name.Length > MaxName || name.Any(char.IsControl))
        {
            throw ApiException.Validation($"A connection needs a name of at most {MaxName} characters.", "name");
        }
        return (name, ReceiverUrl(body?.ReceiverUrl));
    }

    // https, or http on localhost; no credentials or fragment.
    private static string ReceiverUrl(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        var ok = text.Length <= MaxUrl
            && Uri.TryCreate(text, UriKind.Absolute, out var uri)
            && uri.UserInfo.Length == 0
            && uri.Fragment.Length == 0
            && (uri.Scheme == Uri.UriSchemeHttps || (uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback));
        return ok ? text : throw ApiException.Validation("The receiver URL must be an https address, or an http address on localhost, with no credentials.", "receiverUrl");
    }

    private static async Task RequireFreeNameAsync(NpgsqlConnection connection, string name, Guid? except, CancellationToken cancellationToken)
    {
        var taken = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM agent_connection WHERE lower(name) = lower(@name) AND archived_at IS NULL AND id IS DISTINCT FROM @except)",
            new { name, except }, cancellationToken: cancellationToken));
        if (taken)
        {
            throw ApiException.Validation("Another connection already has that name.", "name");
        }
    }

    private static async Task RequireLiveAsync(NpgsqlConnection connection, Guid id, CancellationToken cancellationToken)
    {
        var current = await ReadAsync(connection, id, cancellationToken);
        if (current.ArchivedAt is not null)
        {
            throw ApiException.Conflict("That connection is archived.");
        }
    }

    private static async Task<AgentConnectionDto> ReadAsync(NpgsqlConnection connection, Guid id, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<AgentConnectionRow>(new CommandDefinition(
            $"SELECT {AgentConnectionRow.Columns} FROM agent_connection WHERE id = @id", new { id }, cancellationToken: cancellationToken));
        return row?.ToDto() ?? throw ApiException.NotFound("There is no such agent connection.");
    }
}
