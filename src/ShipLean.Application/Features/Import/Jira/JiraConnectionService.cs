using Agoda.IoC.Core;
using Dapper;
using Microsoft.Extensions.Configuration;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Features.Import.Jira;

// A project's Jira connection: save (which replaces), read, test, and the project list. The caller checks project admin
// access first. The token goes in encrypted and never comes out: a read gets its last four characters, stored beside it, so reading never decrypts.
[RegisterPerRequest]
public sealed class JiraConnectionService(
    NpgsqlDataSource dataSource, JiraTokens tokens, IJiraClient jira, ICurrentUser currentUser, IConfiguration configuration, TimeProvider clock)
{
    private sealed record Row(
        Guid Id, Guid ProjectId, string BaseUrl, string Kind, string Email, string? ApiTokenEncrypted, string? TokenLastFour, DateTimeOffset CreatedAt,
        Guid? CreatorId, string? CreatorName, string? CreatorInitials, string? CreatorColour, string? CreatorPhotoUrl);

    private sealed record ImportedRow(string JiraProjectKey, string ProjectKey, Guid JobId, DateTimeOffset FinishedAt);

    private static readonly string CurrentSql =
        $"""
        SELECT c.id, c.project_id AS projectId, c.base_url AS baseUrl, c.kind, c.email, c.api_token_encrypted AS apiTokenEncrypted, c.token_last_four AS tokenLastFour, c.created_at AS createdAt,
               u.id AS creatorId, u.name AS creatorName, u.initials AS creatorInitials, u.avatar_colour AS creatorColour, {UserRefs.PhotoUrlSql} AS creatorPhotoUrl
        FROM jira_connection c
        LEFT JOIN app_user u ON u.id = c.created_by
        WHERE c.project_id = @projectId AND c.archived_at IS NULL
        """;

    public async Task<JiraConnectionDto?> GetAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(CurrentSql, new { projectId }, cancellationToken: cancellationToken));
        return row is null ? null : ToDto(row);
    }

    // Always a new row: the previous current connection is archived (and its token cleared) in the same transaction, so a replaced connection keeps its history and its import jobs keep their foreign key.
    public async Task<JiraConnectionDto> SaveAsync(Guid projectId, JiraConnectionPut put, CancellationToken cancellationToken)
    {
        var (baseUrl, email, token) = Validated(put);
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // Two admins saving at once take turns on the project row, so the one-current-connection index never trips.
        await connection.ExecuteAsync(new CommandDefinition("SELECT id FROM project WHERE id = @projectId FOR UPDATE", new { projectId }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE jira_connection SET archived_at = @now, updated_at = @now, api_token_encrypted = NULL WHERE project_id = @projectId AND archived_at IS NULL",
            new { projectId, now }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO jira_connection (id, project_id, base_url, kind, email, api_token_encrypted, token_last_four, created_by, created_at, updated_at)
            VALUES (@id, @projectId, @baseUrl, @kind, @email, @encrypted, @lastFour, @createdBy, @now, @now)
            """,
            new { id = Guid.NewGuid(), projectId, baseUrl, kind = JiraKinds.Column(put.Kind), email, encrypted = tokens.Protect(token), lastFour = JiraTokens.LastFour(token), createdBy = currentUser.Id, now },
            transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return (await GetAsync(projectId, cancellationToken))!;
    }

    // Who the saved connection signs in as. A connection that does not work is a result, not an error, and so is having none.
    public async Task<JiraTestResultDto> TestAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var credentials = await CredentialsAsync(projectId, cancellationToken);
        if (credentials is null)
        {
            return new JiraTestResultDto(false, null, "No Jira connection is saved yet. Save the address and token first.");
        }
        try
        {
            var me = await jira.GetMyselfAsync(credentials, cancellationToken);
            return new JiraTestResultDto(true, new JiraWhoDto(me.DisplayName, me.Email), null);
        }
        catch (ApiException error) when (error.Code is ApiErrorCodes.Validation or ApiErrorCodes.NotConfigured)
        {
            return new JiraTestResultDto(false, null, error.Message);
        }
    }

    public async Task<IReadOnlyList<JiraProjectDto>> ListProjectsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        var credentials = await CredentialsAsync(projectId, cancellationToken)
            ?? throw ApiException.Validation("Save a Jira connection before listing its projects.");
        var projects = await jira.ListProjectsAsync(credentials, cancellationToken);
        var imported = await ImportedAsync(projectId, cancellationToken);
        return [.. projects.Select(p => new JiraProjectDto(p.Key, p.Name, p.IssueCount, imported.GetValueOrDefault(p.Key)))];
    }

    private (string BaseUrl, string Email, string Token) Validated(JiraConnectionPut put)
    {
        if (!Enum.IsDefined(put.Kind))
        {
            throw ApiException.Validation("The Jira kind must be cloud or server.", "kind");
        }
        var baseUrl = JiraEndpointPolicy.Parse(put.BaseUrl, JiraEndpointPolicy.AllowsLocal(configuration)).AbsoluteUri.TrimEnd('/');
        var email = (put.Email ?? string.Empty).Trim();
        if (put.Kind == JiraKind.Cloud && !email.Contains('@'))
        {
            throw ApiException.Validation("Jira Cloud needs the email of the account that created the API token.", "email");
        }
        var token = (put.Token ?? string.Empty).Trim();
        return token.Length == 0 ? throw ApiException.Validation("The Jira token can't be blank.", "token") : (baseUrl, email, token);
    }

    private async Task<JiraCredentials?> CredentialsAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(CurrentSql, new { projectId }, cancellationToken: cancellationToken));
        return row?.ApiTokenEncrypted is { } token ? new JiraCredentials(row.BaseUrl, JiraKinds.Parse(row.Kind), row.Email, tokens.Unprotect(token)) : null;
    }

    // The latest finished import of each Jira project, through any connection this project has had.
    private async Task<Dictionary<string, ImportedAsDto>> ImportedAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ImportedRow>(new CommandDefinition(
            """
            SELECT DISTINCT ON (j.jira_project_key) j.jira_project_key AS jiraProjectKey, j.project_key AS projectKey, j.id AS jobId, j.finished_at AS finishedAt
            FROM import_job j
            JOIN jira_connection c ON c.id = j.connection_id
            WHERE c.project_id = @projectId AND j.status IN ('done', 'doneWithErrors') AND j.finished_at IS NOT NULL
            ORDER BY j.jira_project_key, j.finished_at DESC
            """,
            new { projectId }, cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.JiraProjectKey, r => new ImportedAsDto(r.ProjectKey, r.JobId, r.FinishedAt, DisplayDate.Relative(r.FinishedAt, clock)));
    }

    private JiraConnectionDto ToDto(Row row)
        => new(
            row.Id, row.ProjectId, row.BaseUrl, JiraKinds.Parse(row.Kind), row.Email,
            row.TokenLastFour ?? string.Empty,
            row.CreatorId is { } creator ? new UserRef(creator, row.CreatorName!, row.CreatorInitials!, row.CreatorColour!, row.CreatorPhotoUrl) : null,
            row.CreatedAt, DisplayDate.Relative(row.CreatedAt, clock));
}
