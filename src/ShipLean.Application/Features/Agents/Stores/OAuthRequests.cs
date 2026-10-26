using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Agents.Stores;

public sealed class OAuthRequests(NpgsqlDataSource dataSource, TimeProvider clock) : IOAuthRequests
{
    public async Task<Guid> CreateAsync(OAuthRequestCreate request, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var inserted = await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO oauth_request (id, client_id, redirect_uri, state, code_challenge, scopes, resource, user_id, expires_at)
            SELECT @id, c.client_id, @redirectUri, @state, @codeChallenge, @scopes, @resource, u.id, @expiresAt
            FROM oauth_client c, app_user u
            WHERE c.client_id = @clientId AND c.archived_at IS NULL AND u.id = @userId
            """,
            new
            {
                id,
                clientId = request.ClientId,
                redirectUri = request.RedirectUri,
                state = request.State,
                codeChallenge = request.CodeChallenge,
                scopes = ScopeLists.Sorted(request.Scopes.ToArray()),
                resource = request.Resource,
                userId = request.UserId,
                expiresAt = clock.GetUtcNow() + OAuthRequestCreate.Lifetime,
            },
            cancellationToken: cancellationToken));
        return inserted == 1 ? id : throw ApiException.NotFound("No such client or user.");
    }
}
