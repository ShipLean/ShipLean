using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.Application.Features.Retros.Analysis.Record;

// What the analysis writer reads that the decide reader does not: which points an earlier analysis covered, a pattern's evidence, the earlier patterns a new one may continue,
// and the work item ids of action keys. The covered and evidence rules are the shared SQL fragments.
internal sealed class RetroAnalysisReads(NpgsqlDataSource dataSource)
{
    private static readonly string CoveredPointsSql = $"SELECT p.id FROM retro_point p WHERE p.id = ANY(@pointIds) AND {RetroCoveredSql.Covered("p")}";

    private static readonly string EvidenceSql =
        $"SELECT e.points, e.items, e.features, e.sprints, e.agents, e.first_logged AS firstLogged, e.last_logged AS lastLogged FROM ({RetroEvidenceSql.ForPoints()}) e";

    public async Task<HashSet<Guid>> CoveredAsync(Guid projectId, IReadOnlyCollection<Guid> pointIds, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var covered = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                CoveredPointsSql,
                new { projectId, pointIds = pointIds.ToArray() },
                cancellationToken: cancellationToken));
        return [.. covered];
    }

    public async Task<RetroEvidence> EvidenceAsync(IReadOnlyList<Guid> pointIds, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleAsync<EvidenceRow>(
            new CommandDefinition(
                EvidenceSql,
                new { pointIds = pointIds.ToArray() },
                cancellationToken: cancellationToken));
        return new RetroEvidence(row.Points, Strings(row.Items), Strings(row.Features), Strings(row.Sprints), Strings(row.Agents), Utc(row.FirstLogged), Utc(row.LastLogged));
    }

    // The ids among those given that are patterns of a live analysis of this project made with this embedding model.
    public async Task<HashSet<Guid>> ContinuablePatternsAsync(Guid projectId, string embeddingModel, IReadOnlyCollection<Guid> patternIds, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var found = await connection.QueryAsync<Guid>(
            new CommandDefinition(
                """
                SELECT rp.id FROM retro_pattern rp JOIN retro_analysis ra ON ra.id = rp.analysis_id
                WHERE rp.id = ANY(@patternIds) AND ra.project_id = @projectId AND ra.embedding_model = @embeddingModel AND ra.archived_at IS NULL
                """,
                new { projectId, embeddingModel, patternIds = patternIds.ToArray() },
                cancellationToken: cancellationToken));
        return [.. found];
    }

    public async Task<IReadOnlyDictionary<string, Guid>> WorkItemIdsAsync(Guid projectId, IReadOnlyCollection<string> keys, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<KeyRow>(
            new CommandDefinition("SELECT key, id FROM work_item WHERE project_id = @projectId AND key = ANY(@keys)", new { projectId, keys = keys.ToArray() }, cancellationToken: cancellationToken));
        return rows.ToDictionary(row => row.Key, row => row.Id, StringComparer.Ordinal);
    }

    private static DateTimeOffset? Utc(DateTime? value) => value is { } time ? new DateTimeOffset(DateTime.SpecifyKind(time, DateTimeKind.Utc)) : null;

    private static string[] Strings(Array values) => [.. values.Cast<string>()];

    private sealed record EvidenceRow(int Points, Array Items, Array Features, Array Sprints, Array Agents, DateTime? FirstLogged, DateTime? LastLogged);

    private sealed record KeyRow(string Key, Guid Id);
}
