using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;

namespace ShipLean.Application.Features.Import.Jira;

// The credentials of a current (not replaced) connection, decrypted for the importer. Callers check project access first.
public sealed class JiraConnections(NpgsqlDataSource dataSource, JiraTokens tokens) : IJiraConnections
{
    private sealed record CredentialsRow(string BaseUrl, string Kind, string Email, string? ApiTokenEncrypted);

    public async Task<JiraCredentials> GetCredentialsAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<CredentialsRow>(new CommandDefinition(
            "SELECT base_url AS baseUrl, kind, email, api_token_encrypted AS apiTokenEncrypted FROM jira_connection WHERE id = @connectionId AND archived_at IS NULL",
            new { connectionId }, cancellationToken: cancellationToken));
        return row is { ApiTokenEncrypted: { } token }
            ? new JiraCredentials(row.BaseUrl, JiraKinds.Parse(row.Kind), row.Email, tokens.Unprotect(token))
            : throw ApiException.NotFound("That Jira connection does not exist.");
    }
}
