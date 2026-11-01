using Dapper;
using Npgsql;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Retros.Theming;

// The production themer (C5, D9). When SHIPLEAN_RETRO_AUTO_THEMES is on and the embedder is semantic it clusters a point into the live theme whose centroid is
// nearest by cosine similarity, if that is at least JoinThreshold, and otherwise starts a theme named from the point. It
// returns the point's embedding for the writer to store. Otherwise (the switch is off, which is the default, or the embedder is not semantic) it delegates to PickRetroThemer.
//
// A theme an agent supplies in auto mode is ignored without a warning here; RetroLog adds that warning itself.
// It never moves an existing point: assignment happens on insert only.
public sealed class RetroThemer(NpgsqlDataSource dataSource, IEmbedder embedder, PickRetroThemer pick, bool autoEnabled) : IRetroThemer
{
    // multilingual-e5-small cosines for retro text sit between 0.80 and 0.91, so only the narrow band 0.86-0.89 separates anything.
    // Measured on retro-theme-fixture.json (33 points, 7 themes, pairwise). Through this themer at 0.88 (stored centroids, as
    // in production): 15 themes, precision 34.1%, recall 22.6%, F1 about 27%. A simulated sweep over the same vectors with
    // exact running means reaches precision 45.5%, recall 24.2%, F1 about 32% at 0.88, the best of any threshold; none reaches
    // 80% on both. Auto clustering is therefore off by default (SHIPLEAN_RETRO_AUTO_THEMES).
    public const float JoinThreshold = 0.88f;

    private const int NameAttempts = 5;

    public RetroThemeMode Mode => IsAuto ? RetroThemeMode.Auto : RetroThemeMode.Pick;

    private bool IsAuto => autoEnabled && embedder.Status.Mode == EmbedderMode.Semantic && embedder.ModelId is not null;

    public static string PassageText(string text, string? rootCause)
        => string.IsNullOrWhiteSpace(rootCause) ? text : $"{text} — {rootCause}";

    public async Task<RetroThemeResult> AssignAsync(RetroThemeRequest request, CancellationToken cancellationToken)
    {
        if (!IsAuto)
        {
            return await pick.AssignAsync(request, cancellationToken);
        }

        var model = embedder.ModelId!.ToString();
        var embedding = (await embedder.EmbedPassagesAsync([PassageText(request.Text, request.RootCause)], cancellationToken))[0];
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var themes = await ThemeCentroids.LoadLiveAsync(connection, request.ProjectId, cancellationToken);
        var nearest = themes
            .Where(t => ThemeCentroids.IsComparable(t, model, embedding.Length))
            .Select(t => (Theme: t, Score: ThemeCentroids.Cosine(t.Centroid!, embedding)))
            .OrderByDescending(x => x.Score)
            .FirstOrDefault();

        Guid themeId;
        if (nearest.Theme is not null && nearest.Score >= JoinThreshold)
        {
            themeId = nearest.Theme.Id;
            await ThemeCentroids.FoldAsync(dataSource, themeId, embedding, model, cancellationToken);
        }
        else
        {
            themeId = await CreateAsync(connection, request, embedding, model, themes, cancellationToken);
        }

        await ThemeCentroids.ReseedAsync(dataSource, embedder, themes.Where(t => !ThemeCentroids.IsComparable(t, model, embedding.Length)), cancellationToken);
        return new RetroThemeResult(themeId, RetroThemeSetBy.Auto, embedding, null);
    }

    private static async Task<Guid> CreateAsync(
        NpgsqlConnection connection, RetroThemeRequest request, float[] embedding, string model, IReadOnlyList<ThemeCentroids.Candidate> themes, CancellationToken cancellationToken)
    {
        var names = themes.Select(t => t.Name).ToList();
        for (var attempt = 0; attempt < NameAttempts; attempt++)
        {
            var name = ThemeNamer.Name(request.Text, names);
            var created = await connection.ExecuteScalarAsync<Guid?>(
                new CommandDefinition(
                    """
                    INSERT INTO retro_theme (id, project_id, name, name_set_by, tone, centroid, centroid_model)
                    VALUES (@id, @projectId, @name, 'auto', 'bad', @centroid, @model)
                    ON CONFLICT (project_id, lower(name)) WHERE archived_at IS NULL AND merged_into_id IS NULL DO NOTHING
                    RETURNING id
                    """,
                    new { id = Guid.NewGuid(), projectId = request.ProjectId, name, centroid = embedding, model },
                    cancellationToken: cancellationToken));
            if (created is { } id)
            {
                return id;
            }

            // Another request took the name first; read the live names again and number past it.
            names = [.. (await ThemeCentroids.LoadLiveAsync(connection, request.ProjectId, cancellationToken)).Select(t => t.Name)];
        }

        throw new InvalidOperationException($"Could not find a free auto theme name for project {request.ProjectId}.");
    }
}
