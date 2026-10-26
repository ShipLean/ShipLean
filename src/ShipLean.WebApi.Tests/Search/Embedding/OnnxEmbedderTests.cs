using System.Text.Json;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.Search.Embedding;

// Runs alone: the real model is ~700 MB resident while loaded, and CI's 2 GB runners can't hold several next to the parallel hosts.
[TestFixture]
[NonParallelizable]
public class OnnxEmbedderTests
{
    private OnnxEmbedder _embedder = null!;

    [OneTimeSetUp]
    public void LoadTheModel() => _embedder = RealModel.Load();

    [OneTimeTearDown]
    public void DisposeTheModel() => _embedder.Dispose();

    [Test]
    public async Task Embedder_WhenRealModel_ShouldRankParaphraseAboveUnrelated()
    {
        var query = await _embedder.EmbedQueryAsync("Users cannot log in");
        var passages = await _embedder.EmbedPassagesAsync(["Sign-in fails after the password reset", "The catering budget for the quarter"]);

        Dot(query, passages[0]).ShouldBeGreaterThan(Dot(query, passages[1]) + 0.05f);
    }

    [Test]
    public void Embedder_WhenTokenized_ShouldMatchTransformersIds()
    {
        var path = Path.Combine(RepoPaths.Root, "src", "ShipLean.WebApi.Tests", "Search", "Fixtures", "e5-token-ids.json");
        using var document = JsonDocument.Parse(File.ReadAllText(path));
        var tokenizer = new E5Tokenizer(Path.Combine(RealModel.Directory, "sentencepiece.bpe.model"));

        foreach (var testCase in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            var text = testCase.GetProperty("text").GetString()!;
            var expected = testCase.GetProperty("ids").EnumerateArray().Select(x => x.GetInt32()).ToArray();

            tokenizer.Encode(text, 512).ToArray().ShouldBe(expected, $"ids for \"{text}\"");
        }
    }

    [Test]
    public async Task Embedder_WhenTextLongerThanWindow_ShouldTruncateNotThrow()
    {
        var longText = string.Join(' ', Enumerable.Repeat("sprint planning moved to thursday", 600));
        var manifest = ModelManifest.Read(RealModel.Directory);
        var tokenizer = new E5Tokenizer(Path.Combine(RealModel.Directory, "sentencepiece.bpe.model"));

        var vector = (await _embedder.EmbedPassagesAsync([longText]))[0];

        vector.Length.ShouldBe(_embedder.Dimensions);
        var ids = tokenizer.Encode(manifest.PassagePrefix + longText, manifest.MaxTokens);
        ids.Count.ShouldBe(manifest.MaxTokens);
        ids[^1].ShouldBe(E5Tokenizer.EndId);
        var prefix = tokenizer.Encode(manifest.PassagePrefix, manifest.MaxTokens).SkipLast(1).ToArray();
        ids.Take(prefix.Length).ToArray().ShouldBe(prefix);
    }

    [Test]
    public async Task Embedder_WhenMoreTextsThanBatch_ShouldReturnOneVectorEach()
    {
        var texts = Enumerable.Range(0, 70).Select(i => $"item number {i}").ToList();

        var vectors = await _embedder.EmbedPassagesAsync(texts);

        vectors.Count.ShouldBe(70);
    }

    [Test]
    public async Task Embed_WhenBatched_ShouldReportDrift()
    {
        string[] texts = ["Sign-in fails after the password reset", "The catering budget for the quarter", string.Join(' ', Enumerable.Repeat("long text", 200))];
        var batch = await _embedder.EmbedPassagesAsync(texts);

        var worst = 0f;
        for (var i = 0; i < texts.Length; i++)
        {
            var single = (await _embedder.EmbedPassagesAsync([texts[i]]))[0];
            worst = Math.Max(worst, 1f - Dot(batch[i], single));
        }

        await TestContext.Out.WriteLineAsync($"worst batch-vs-single cosine drift: {worst:E2}");
        worst.ShouldBeLessThan(5e-2f);
    }

    [Test]
    public async Task Embed_WhenCalledConcurrently_ShouldMatchSequential()
    {
        var queries = Enumerable.Range(0, 8).Select(i => $"login problem number {i} after the reset").ToList();
        var passages = Enumerable.Range(0, 70).Select(i => $"item number {i} about sprint planning").ToList();
        var expectedQueries = new List<float[]>();
        foreach (var query in queries)
        {
            expectedQueries.Add(await _embedder.EmbedQueryAsync(query));
        }
        var expectedPassages = await _embedder.EmbedPassagesAsync(passages);

        var queryTasks = queries.Select(q => _embedder.EmbedQueryAsync(q)).ToList();
        var passageTask = _embedder.EmbedPassagesAsync(passages);
        var actualQueries = await Task.WhenAll(queryTasks);
        var actualPassages = await passageTask;

        for (var i = 0; i < queries.Count; i++)
        {
            (1f - Dot(actualQueries[i], expectedQueries[i])).ShouldBeLessThan(1e-3f);
        }
        for (var i = 0; i < passages.Count; i++)
        {
            (1f - Dot(actualPassages[i], expectedPassages[i])).ShouldBeLessThan(1e-3f);
        }
    }

    private static float Dot(float[] a, float[] b) => a.Zip(b, (x, y) => x * y).Sum();
}
