using Dapper;
using Npgsql;
using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Servings;

// Writes on its own connection: a serving never rolls back with, or waits on, a caller's transaction.
public sealed class DbPromptServingLog(NpgsqlDataSource dataSource) : IPromptServingLog
{
    public async Task RecordAsync(PromptServing serving, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var prompt = PromptKeys.ToWire(serving.Key);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO prompt_serving (id, project_id, prompt, version, version_id, built_in_hash, agent, client_name, client_source, item_key, user_id, served_at)
            VALUES (@id, @projectId, @prompt, @version,
                    (SELECT id FROM prompt_version WHERE project_id = @projectId AND prompt = @prompt AND number = @version),
                    @hash, @agent, @clientName, @clientSource, @itemKey, @userId, @servedAt)
            """,
            new
            {
                id = Guid.NewGuid(), projectId = serving.ProjectId, prompt, version = serving.Version, hash = serving.BuiltInHash, agent = serving.Agent,
                clientName = serving.ClientName, clientSource = serving.ClientSource, itemKey = serving.ItemKey, userId = serving.UserId,
                servedAt = serving.ServedAt.UtcDateTime,
            },
            cancellationToken: cancellationToken));
    }
}
