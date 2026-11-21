using Dapper;
using Npgsql;
using ShipLean.Application.Features.Prompts;
using ShipLean.Contracts.Prompts;

namespace ShipLean.WebApi.Features.Prompts;

// P5: every built-in text ShipLean has run with is kept, so "Built-in updated" can show the built-in's own diff. At startup the 11 current
// built-ins are stored (hash, text, first seen) unless already there. StartAsync awaits the inserts, so the rows exist before the first request.
public sealed class PromptBuiltInSnapshotter(NpgsqlDataSource dataSource, IBuiltInPrompts builtIns, TimeProvider time, ILogger<PromptBuiltInSnapshotter> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var now = time.GetUtcNow().UtcDateTime;
        var stored = 0;
        foreach (var key in Enum.GetValues<PromptKey>())
        {
            stored += await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO prompt_built_in (prompt, hash, text, first_seen_at)
                VALUES (@prompt, @hash, @text, @now)
                ON CONFLICT DO NOTHING
                """,
                new { prompt = PromptKeys.ToWire(key), hash = builtIns.Hash(key), text = builtIns.Text(key).Replace("\r\n", "\n", StringComparison.Ordinal), now },
                cancellationToken: cancellationToken));
        }
        logger.LogInformation("Prompt built-ins snapshot: {Stored} new of {Total}", stored, Enum.GetValues<PromptKey>().Length);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
