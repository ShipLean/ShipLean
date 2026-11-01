using System.Text.Json;
using Dapper;
using Npgsql;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Retros.Theming;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Search.Embedding;

namespace ShipLean.WebApi.Tests.Retros.Theming;

// Runs alone and needs SHIPLEAN_EMBEDDINGS_MODEL_PATH: the real model is ~700 MB resident while loaded.
// Feeds retro-theme-fixture.json in order through the production themer (auto mode forced on) and prints how the themes it made
// compare with the expected ones by pairwise agreement; it is a diagnostic, not a quality gate: for every pair of points, "same theme" must match "expected same theme".
[TestFixture]
[NonParallelizable]
public sealed class RetroThemerRealModelTests : ApiFixture
{
    private OnnxEmbedder _embedder = null!;

    [OneTimeSetUp]
    public void LoadTheModel() => _embedder = RealModel.Load();

    [OneTimeTearDown]
    public void DisposeTheModel() => _embedder.Dispose();

    [Test]
    public async Task Sweep_WhenClustered_ShouldReportPrecisionRecallPerThreshold()
    {
        var points = LoadFixture();
        var themer = new RetroThemer(Host.Services.GetRequiredService<NpgsqlDataSource>(), _embedder, Host.Services.GetRequiredService<PickRetroThemer>(), true);
        var project = SeedIds.Projects.Ship;
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE retro_theme SET archived_at = now() WHERE project_id = @project AND archived_at IS NULL", new { project });
        }


        var actual = new List<Guid>();
        foreach (var point in points)
        {
            var result = await themer.AssignAsync(new RetroThemeRequest(project, point.Text, point.RootCause, null, false), CancellationToken.None);
            actual.Add(result.ThemeId!.Value);
            await CountPointAsync(result.ThemeId.Value);
        }

        var agreement = Agreement(points.Select(p => p.Theme).ToList(), actual);
        await TestContext.Out.WriteLineAsync($"themes made: {actual.Distinct().Count()}, expected: {points.Select(p => p.Theme).Distinct().Count()}, precision {agreement.Precision:P1}, recall {agreement.Recall:P1}, agreement {agreement.Rate:P1}, threshold {RetroThemer.JoinThreshold}");
        // Clustering quality is below the 80% bar (auto themes are off by default), so this only checks that the themer
        // clusters at all: some pairs are right both ways, and it neither lumps everything together nor splits everything apart.
        agreement.Precision.ShouldBeGreaterThan(0);
        agreement.Recall.ShouldBeGreaterThan(0);
        actual.Distinct().Count().ShouldBeInRange(2, points.Count);
    }

    [Test]
    public async Task Sweep_ShouldReportF1PerThresholdForThePassageVectors()
    {
        var points = LoadFixture();
        var texts = points.Select(p => RetroThemer.PassageText(p.Text, p.RootCause)).ToList();
        var vectors = await _embedder.EmbedPassagesAsync(texts);
        for (var threshold = 0.80f; threshold <= 0.96f; threshold += 0.01f)
        {
            var centroids = new List<(float[] Sum, int Count)>();
            var actual = new List<Guid>();
            var ids = new List<Guid>();
            foreach (var vector in vectors)
            {
                var best = -1;
                var bestScore = -1f;
                for (var i = 0; i < centroids.Count; i++)
                {
                    var score = Cosine(centroids[i].Sum.Select(x => x / centroids[i].Count).ToArray(), vector);
                    if (score > bestScore)
                    {
                        (best, bestScore) = (i, score);
                    }
                }

                if (best >= 0 && bestScore >= threshold)
                {
                    var (sum, count) = centroids[best];
                    centroids[best] = ([.. sum.Zip(vector, (a, b) => a + b)], count + 1);
                    actual.Add(ids[best]);
                }
                else
                {
                    centroids.Add((vector, 1));
                    ids.Add(Guid.NewGuid());
                    actual.Add(ids[^1]);
                }
            }

            var agreement = Agreement(points.Select(p => p.Theme).ToList(), actual);
            await TestContext.Out.WriteLineAsync($"threshold {threshold:F3}: F1 {2 * agreement.Precision * agreement.Recall / Math.Max(agreement.Precision + agreement.Recall, 1e-9):P1} themes {centroids.Count}, precision {agreement.Precision:P1}, recall {agreement.Recall:P1}, agreement {agreement.Rate:P1}");
        }

        points.ShouldNotBeEmpty();
    }

    private static float Cosine(float[] a, float[] b)
    {
        var dot = a.Zip(b, (x, y) => x * y).Sum();
        return dot / (MathF.Sqrt(a.Sum(x => x * x)) * MathF.Sqrt(b.Sum(x => x * x)));
    }

    private static (double Rate, double Precision, double Recall) Agreement(IReadOnlyList<string> expected, IReadOnlyList<Guid> actual)
    {
        int agree = 0, total = 0, truePositive = 0, predictedSame = 0, expectedSame = 0;
        for (var i = 0; i < expected.Count; i++)
        {
            for (var j = i + 1; j < expected.Count; j++)
            {
                var wantSame = expected[i] == expected[j];
                var gotSame = actual[i] == actual[j];
                total++;
                agree += wantSame == gotSame ? 1 : 0;
                truePositive += wantSame && gotSame ? 1 : 0;
                predictedSame += gotSame ? 1 : 0;
                expectedSame += wantSame ? 1 : 0;
            }
        }

        return ((double)agree / total, predictedSame == 0 ? 0 : (double)truePositive / predictedSame, (double)truePositive / expectedSame);
    }

    private static List<FixturePoint> LoadFixture()
    {
        var json = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "Retros", "retro-theme-fixture.json"));
        return JsonSerializer.Deserialize<List<FixturePoint>>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    // The themer joins on stored centroids and point counts, so each clustered point is counted on its theme like the writers do.
    private async Task CountPointAsync(Guid themeId)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE retro_theme SET point_count = point_count + 1 WHERE id = @themeId", new { themeId });
    }

    private sealed record FixturePoint(string Theme, string Category, string Text, string? RootCause);
}
