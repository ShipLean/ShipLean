using Dapper;
using Npgsql;
using ShipLean.Application.Features.Agents.Clients;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Agents.Stores;

// Reads and spends the oauth_request rows /oauth/authorize wrote. A request is the current user's own, unexpired and
// undecided, or it is not found (one message for every case, so nothing says which).
public sealed class AgentConsent(
    NpgsqlDataSource dataSource,
    IAgentAuthorizations authorizations,
    IOAuthCodes codes,
    IAgentAddresses addresses,
    ICurrentUser currentUser,
    TimeProvider clock) : IAgentConsent
{
    private const string Read = "shiplean:read";
    private const string NotFoundMessage = "No such authorization request.";

    private sealed record Pending(
        Guid Id,
        string ClientId,
        string RedirectUri,
        string? State,
        string CodeChallenge,
        Array Scopes,
        string Resource,
        string Kind,
        string ClientName,
        string? ClientHost,
        Array ClientRedirectUris)
    {
    }

    public async Task<ConsentRequestDto> GetAsync(Guid requestId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var request = await FindAsync(connection, requestId, cancellationToken);
        var client = new ClientRow(request.ClientId, request.Kind, request.ClientName, request.ClientHost, (string[])request.ClientRedirectUris).ToClient();
        return new ConsentRequestDto(
            request.Id, client.ClientName, client.ClientHost, client.Verified, RedirectUriRules.Describe(new Uri(request.RedirectUri)),
            WithRead((string[])request.Scopes), client.RedirectUris.All(OAuthClient.IsLoopback));
    }

    public async Task<ConsentResult> DecideAsync(Guid requestId, ConsentDecision decision, CancellationToken cancellationToken = default)
    {
        var ticked = decision.Scopes ?? [];
        Pending request;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            request = await FindAsync(connection, requestId, cancellationToken);
            if (decision.Allow && ticked.Any(s => s != Read && !((string[])request.Scopes).Contains(s)))
            {
                throw ApiException.Validation("A ticked scope was not requested.");
            }
            // The first decision spends the request; a concurrent second one finds nothing to update.
            var spent = await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE oauth_request SET decided_at = @now WHERE id = @requestId AND user_id = @userId AND decided_at IS NULL AND expires_at > @now",
                new { requestId, userId = currentUser.Id, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
            if (spent != 1)
            {
                throw ApiException.NotFound(NotFoundMessage);
            }
        }

        var query = new List<KeyValuePair<string, string>>();
        if (decision.Allow)
        {
            var granted = new HashSet<string>(ticked, StringComparer.Ordinal) { Read };
            var authorizationId = await authorizations.CreateAsync(currentUser.Id, request.ClientId, granted, cancellationToken);
            var code = await codes.IssueAsync(authorizationId, request.CodeChallenge, request.RedirectUri, request.Resource, cancellationToken);
            query.Add(new("code", code));
        }
        else
        {
            query.Add(new("error", "access_denied"));
        }
        if (request.State is not null)
        {
            query.Add(new("state", request.State));
        }
        query.Add(new("iss", (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer));
        return new ConsentResult(Redirect(request.RedirectUri, query));
    }

    private async Task<Pending> FindAsync(NpgsqlConnection connection, Guid requestId, CancellationToken cancellationToken)
        => await connection.QuerySingleOrDefaultAsync<Pending>(new CommandDefinition(
            """
            SELECT r.id, r.client_id AS clientId, r.redirect_uri AS redirectUri, r.state, r.code_challenge AS codeChallenge,
                   r.scopes, r.resource, c.kind, c.client_name AS clientName, c.client_host AS clientHost,
                   c.redirect_uris AS clientRedirectUris
            FROM oauth_request r
            JOIN oauth_client c ON c.client_id = r.client_id
            WHERE r.id = @requestId AND r.user_id = @userId AND r.decided_at IS NULL AND r.expires_at > @now
            """,
            new { requestId, userId = currentUser.Id, now = clock.GetUtcNow() }, cancellationToken: cancellationToken))
           ?? throw ApiException.NotFound(NotFoundMessage);

    private static IReadOnlyList<string> WithRead(IReadOnlyList<string> scopes)
        => scopes.Contains(Read) ? scopes : [Read, .. scopes];

    private static string Redirect(string redirectUri, List<KeyValuePair<string, string>> query)
    {
        var separator = redirectUri.Contains('?', StringComparison.Ordinal) ? '&' : '?';
        return redirectUri + separator + string.Join('&', query.Select(p => $"{Uri.EscapeDataString(p.Key)}={Uri.EscapeDataString(p.Value)}"));
    }
}
