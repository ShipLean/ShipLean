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
// compare with the expected ones by pairwise agreement;
// it is a diagnostic, not a quality gate. Pairwise: for every pair of points, "same theme" is compared with "expected same theme".
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
        var expected = points.Select(p => p.Theme).ToList();
        var vectors = await _embedder.EmbedPassagesAsync(points.Select(p => RetroThemer.PassageText(p.Text, p.RootCause)).ToList());
        var f1ByThreshold = new SortedDictionary<int, double>();
        for (var step = 80; step <= 96; step++)
        {
            var threshold = step / 100f;
            var actual = Cluster(vectors, threshold, out var themes);
            var agreement = Agreement(expected, actual);
            var f1 = F1(agreement);
            f1ByThreshold[step] = f1;
            await TestContext.Out.WriteLineAsync($"threshold {threshold:F2}: F1 {f1:P1} themes {themes}, precision {agreement.Precision:P1}, recall {agreement.Recall:P1}, agreement {agreement.Rate:P1}");
        }

        // The chosen threshold must still be the best of the sweep, so a model or fixture change that moves the optimum is noticed.
        var chosen = (int)Math.Round(RetroThemer.JoinThreshold * 100);
        f1ByThreshold[chosen].ShouldBe(f1ByThreshold.Values.Max(), "F1 at JoinThreshold is the maximum over the sweep");

        // The same fixture through the production themer, with stored centroids: the figures quoted at JoinThreshold.
        var themer = new RetroThemer(Host.Services.GetRequiredService<NpgsqlDataSource>(), _embedder, Host.Services.GetRequiredService<PickRetroThemer>(), true);
        var project = SeedIds.Projects.Ship;
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE retro_theme SET archived_at = now() WHERE project_id = @project AND archived_at IS NULL", new { project });
        }

        var produced = new List<Guid>();
        foreach (var point in points)
        {
            var result = await themer.AssignAsync(new RetroThemeRequest(project, point.Text, point.RootCause, null, false), CancellationToken.None);
            produced.Add(result.ThemeId!.Value);
            await CountPointAsync(result.ThemeId.Value);
        }

        var production = Agreement(expected, produced);
        await TestContext.Out.WriteLineAsync($"production at {RetroThemer.JoinThreshold}: themes made {produced.Distinct().Count()}, expected {expected.Distinct().Count()}, F1 {F1(production):P1}, precision {production.Precision:P1}, recall {production.Recall:P1}, agreement {production.Rate:P1}");
        production.Precision.ShouldBeGreaterThan(0);
        production.Recall.ShouldBeGreaterThan(0);
        produced.Distinct().Count().ShouldBeInRange(2, points.Count);
    }

    private static List<Guid> Cluster(IReadOnlyList<float[]> vectors, float threshold, out int themes)
    {
        var sums = new List<float[]>();
        var counts = new List<int>();
        var ids = new List<Guid>();
        var actual = new List<Guid>();
        foreach (var vector in vectors)
        {
            var best = -1;
            var bestScore = -1f;
            for (var i = 0; i < sums.Count; i++)
            {
                var score = Cosine(sums[i], vector);
                if (score > bestScore)
                {
                    (best, bestScore) = (i, score);
                }
            }

            if (best >= 0 && bestScore >= threshold)
            {
                sums[best] = [.. sums[best].Zip(vector, (a, b) => a + b)];
                counts[best]++;
                actual.Add(ids[best]);
            }
            else
            {
                sums.Add(vector);
                counts.Add(1);
                ids.Add(Guid.NewGuid());
                actual.Add(ids[^1]);
            }
        }

        themes = sums.Count;
        return actual;
    }

    private static double F1((double Rate, double Precision, double Recall) a)
        => 2 * a.Precision * a.Recall / Math.Max(a.Precision + a.Recall, 1e-9);

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
