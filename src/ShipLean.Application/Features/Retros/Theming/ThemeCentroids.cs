using Dapper;
using Npgsql;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Retros.Theming;

// A theme's centroid is the mean of the embeddings of the points the themer put on it, stored in retro_theme.centroid together
// with the model that made it (centroid_model). A centroid from another model or another dimension is stale: it is never
// compared, and Reseed rebuilds it from the theme's live points.
//
// The themer runs on its own connection while the writer's transaction may already hold a lock on the same theme row (the
// writer updates point_count on every insert, a person's write locks the theme). Every write here therefore takes the row with
// FOR NO KEY UPDATE SKIP LOCKED in a short transaction of its own and skips a locked theme instead of waiting on it, so the
// themer can never wait on the transaction that called it. A skipped update is not lost for good: the weights come from the
// stored centroid, and a later point or a reseed brings it back in line.
internal static class ThemeCentroids
{
    private const int ReseedThemesPerCall = 5;
    private const int ReseedPointsPerTheme = 20;

    internal sealed record Candidate(Guid Id, string Name, float[]? Centroid, string? CentroidModel, int PointCount);

    private const string CandidateColumns = "id, name, centroid, centroid_model, point_count";

    public static async Task<IReadOnlyList<Candidate>> LoadLiveAsync(NpgsqlConnection connection, Guid projectId, CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(
                $"""
                SELECT {CandidateColumns}
                FROM retro_theme
                WHERE project_id = @projectId AND archived_at IS NULL AND merged_into_id IS NULL
                """,
                new { projectId },
                cancellationToken: cancellationToken));
        return await ReadAsync(reader, cancellationToken);
    }

    public static bool IsComparable(Candidate theme, string model, int dimensions)
        => theme.Centroid is { } centroid && theme.CentroidModel == model && centroid.Length == dimensions;

    public static float Cosine(float[] a, float[] b)
    {
        float dot = 0, normA = 0, normB = 0;
        for (var i = 0; i < a.Length; i++)
        {
            dot += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        return normA == 0 || normB == 0 ? 0 : dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
    }

    // Folds one embedding into the centroid as a running mean weighted by the theme's point count. Skips a locked theme.
    public static async Task FoldAsync(NpgsqlDataSource dataSource, Guid themeId, float[] embedding, string model, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var current = await LockAsync(connection, transaction, themeId, cancellationToken);
        if (current is null)
        {
            return;
        }

        var centroid = current.Centroid is { } old && current.CentroidModel == model && old.Length == embedding.Length
            ? Mean(old, Math.Max(current.PointCount, 1), embedding)
            : embedding;
        await StoreAsync(connection, transaction, themeId, centroid, model, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    // Rebuilds up to a few stale themes of the project from their live points, embedded with the current model.
    public static async Task ReseedAsync(NpgsqlDataSource dataSource, IEmbedder embedder, IEnumerable<Candidate> stale, CancellationToken cancellationToken)
    {
        var model = embedder.ModelId!.ToString();
        foreach (var themeId in stale.Where(t => t.PointCount > 0).Take(ReseedThemesPerCall).Select(t => t.Id))
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var points = (await connection.QueryAsync<(string Text, string? RootCause)>(
                new CommandDefinition(
                    """
                    SELECT text, root_cause AS rootCause FROM retro_point
                    WHERE theme_id = @id AND archived_at IS NULL
                    ORDER BY created_at DESC LIMIT @limit
                    """,
                    new { id = themeId, limit = ReseedPointsPerTheme },
                    cancellationToken: cancellationToken))).ToList();
            if (points.Count == 0)
            {
                continue;
            }

            var vectors = await embedder.EmbedPassagesAsync([.. points.Select(p => RetroThemer.PassageText(p.Text, p.RootCause))], cancellationToken);
            var centroid = new float[vectors[0].Length];
            foreach (var vector in vectors)
            {
                for (var i = 0; i < centroid.Length; i++)
                {
                    centroid[i] += vector[i] / vectors.Count;
                }
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            if (await LockAsync(connection, transaction, themeId, cancellationToken) is not null)
            {
                await StoreAsync(connection, transaction, themeId, centroid, model, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static float[] Mean(float[] centroid, int count, float[] embedding)
    {
        var result = new float[centroid.Length];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = (centroid[i] * count + embedding[i]) / (count + 1);
        }

        return result;
    }

    private static async Task<Candidate?> LockAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid themeId, CancellationToken cancellationToken)
    {
        await using var reader = await connection.ExecuteReaderAsync(
            new CommandDefinition(
                $"""
                SELECT {CandidateColumns}
                FROM retro_theme
                WHERE id = @themeId AND archived_at IS NULL AND merged_into_id IS NULL
                FOR NO KEY UPDATE SKIP LOCKED
                """,
                new { themeId },
                transaction,
                cancellationToken: cancellationToken));
        return (await ReadAsync(reader, cancellationToken)).FirstOrDefault();
    }

    private static async Task<IReadOnlyList<Candidate>> ReadAsync(System.Data.Common.DbDataReader reader, CancellationToken cancellationToken)
    {
        var themes = new List<Candidate>();
        while (await reader.ReadAsync(cancellationToken))
        {
            themes.Add(new Candidate(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.IsDBNull(2) ? null : reader.GetFieldValue<float[]>(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt32(4)));
        }

        return themes;
    }

    private static Task StoreAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid themeId, float[] centroid, string model, CancellationToken cancellationToken)
        => connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE retro_theme SET centroid = @centroid, centroid_model = @model WHERE id = @themeId",
                new { themeId, centroid, model },
                transaction,
                cancellationToken: cancellationToken));
}
