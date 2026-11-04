using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Brand;

// A project's design tokens (project_demo_settings.tokens). The text is parsed by DemoTokens and only the normalised lines are kept; a refused text
// changes nothing. Blank clears them. The settings row is created if the project has none yet.
public sealed class DemoTokensStore(NpgsqlDataSource dataSource, ICurrentUser currentUser, TimeProvider clock) : IDemoTokensStore
{
    public async Task<string> ReadAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
            "SELECT tokens FROM project_demo_settings WHERE project_id = @projectId",
            new { projectId }, cancellationToken: cancellationToken)) ?? string.Empty;
    }

    public async Task SaveAsync(Guid projectId, string? tokens, CancellationToken cancellationToken = default)
    {
        var parsed = DemoTokens.Parse(tokens);
        if (parsed.IsRefused)
        {
            throw ApiException.Validation(parsed.Refusal!, "tokens");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO project_demo_settings (project_id, tokens, updated_by, updated_at)
            VALUES (@projectId, @lines, @userId, @now)
            ON CONFLICT (project_id) DO UPDATE SET tokens = @lines, updated_by = @userId, updated_at = @now
            """,
            new { projectId, lines = parsed.Lines, userId = currentUser.Id, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
    }
}
