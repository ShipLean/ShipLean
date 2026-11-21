using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;
using ShipLean.Core;

namespace ShipLean.Application.Features.Prompts.Servings;

// D19: a serving is shown on a run by its item and its time, never by user.
public sealed class DbPromptServingReads(NpgsqlDataSource dataSource, IBuiltInPrompts builtIns, IViewerZone viewerZone, TimeProvider clock) : IPromptServingReads
{
    private const int MaxServings = 20;

    public async Task<IReadOnlyList<PromptServingDto>> ForRunAsync(
        string projectKey,
        IReadOnlyList<string> itemKeys,
        string? agent,
        DateTimeOffset from,
        DateTimeOffset to,
        CancellationToken cancellationToken = default)
    {
        if (itemKeys.Count == 0)
        {
            return [];
        }
        var agentFilter = agent is not null && agent != PromptAgents.Other && PromptAgents.All.Contains(agent) ? agent : null;
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ServingRow>(new CommandDefinition(
            """
            SELECT s.prompt, s.version, s.agent, s.served_at AS ServedAt
            FROM prompt_serving s
            JOIN project p ON p.id = s.project_id
            WHERE p.key = @projectKey
              AND s.item_key = ANY(@itemKeys)
              AND s.served_at >= @from AND s.served_at <= @to
              AND (@agent::text IS NULL OR s.agent = @agent)
            ORDER BY s.served_at DESC
            LIMIT @limit
            """,
            new { projectKey, itemKeys = itemKeys.ToArray(), from = from.UtcDateTime, to = to.UtcDateTime, agent = agentFilter, limit = MaxServings },
            cancellationToken: cancellationToken));
        var now = clock.GetUtcNow();
        return [.. rows.Select(row => ToDto(row, now))];
    }

    private PromptServingDto ToDto(ServingRow row, DateTimeOffset now)
    {
        PromptKeys.TryParse(row.Prompt, out var key);
        var file = builtIns.Facts(key).File;
        var which = row.Version is { } version ? $"custom v{version}" : "built-in";
        var servedAt = new DateTimeOffset(DateTime.SpecifyKind(row.ServedAt, DateTimeKind.Utc));
        return new PromptServingDto(
            key, file, row.Version, row.Agent, $"{file} {which} · {PromptAgents.Label(row.Agent)}",
            $"{DisplayDate.ShortDay(servedAt, now, viewerZone.Zone)}, {DisplayDate.Time(servedAt, viewerZone.Zone)}");
    }

    private sealed record ServingRow(string Prompt, int? Version, string Agent, DateTime ServedAt);
}
