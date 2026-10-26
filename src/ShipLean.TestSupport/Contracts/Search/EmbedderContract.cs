using ShipLean.Contracts.Search;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Search;

// Behaviour every semantic IEmbedder must have: the vectors have the stated size, are unit length, repeat for the same text,
// do not depend on batching, and put a paraphrase nearer than an unrelated text. It assumes nothing about how the embedder
// works or what the scores are, so the fake and the real model run the same fixture. The subclass says how to build one and may
// change the sample texts.
[TestFixture]
public abstract class EmbedderContract
{
    // Unit length and repeat calls are exact to this.
    private const float Tolerance = 1e-3f;

    // A batch may differ from single calls (dynamic quantisation scales depend on the batch), so it has its own, looser bound.
    protected virtual float BatchTolerance => 1e-2f;

    protected abstract IEmbedder CreateEmbedder();

    protected virtual string Query => "login broken";

    protected virtual string Paraphrase => "sign-in fails";

    protected virtual string Unrelated => "quarterly catering budget";

    private IEmbedder _embedder = null!;

    // One instance for the class: a real model is slow and large to load, and the contract does not need a fresh one.
    [OneTimeSetUp]
    public void BuildTheEmbedder() => _embedder = CreateEmbedder();

    [OneTimeTearDown]
    public async Task DisposeTheEmbedder()
    {
        switch (_embedder)
        {
            case IAsyncDisposable asyncDisposable:
                await asyncDisposable.DisposeAsync();
                break;
            case IDisposable disposable:
                disposable.Dispose();
                break;
        }
    }

    [Test]
    public void Status_ShouldBeSemanticWithAModel()
    {
        _embedder.Status.Mode.ShouldBe(EmbedderMode.Semantic);
        _embedder.ModelId.ShouldNotBeNull();
    }

    [Test]
    public async Task Dimensions_ShouldMatchEveryVector()
    {
        _embedder.Dimensions.ShouldBeGreaterThan(0);

        (await _embedder.EmbedQueryAsync(Query)).Length.ShouldBe(_embedder.Dimensions);
        (await _embedder.EmbedPassagesAsync([Paraphrase, Unrelated])).ShouldAllBe(v => v.Length == _embedder.Dimensions);
    }

    [Test]
    public async Task Vectors_ShouldBeUnitLength()
    {
        var passages = await _embedder.EmbedPassagesAsync([Paraphrase, Unrelated, "a"]);

        foreach (var vector in passages.Append(await _embedder.EmbedQueryAsync(Query)))
        {
            Norm(vector).ShouldBe(1f, Tolerance);
        }
    }

    [Test]
    public async Task Embed_WhenRepeated_ShouldBeDeterministic()
    {
        var first = await _embedder.EmbedQueryAsync(Query);
        var second = await _embedder.EmbedQueryAsync(Query);
        var passageFirst = (await _embedder.EmbedPassagesAsync([Paraphrase]))[0];
        var passageSecond = (await _embedder.EmbedPassagesAsync([Paraphrase]))[0];

        Cosine(first, second).ShouldBe(1f, Tolerance);
        Cosine(passageFirst, passageSecond).ShouldBe(1f, Tolerance);
    }

    [Test]
    public async Task EmbedPassages_WhenBatched_ShouldMatchSingleCalls()
    {
        string[] texts = [Paraphrase, Unrelated, Query];

        var batch = await _embedder.EmbedPassagesAsync(texts);

        batch.Count.ShouldBe(texts.Length);
        var singles = new List<float[]>();
        foreach (var text in texts)
        {
            singles.Add((await _embedder.EmbedPassagesAsync([text]))[0]);
        }
        for (var i = 0; i < texts.Length; i++)
        {
            Cosine(batch[i], singles[i]).ShouldBe(1f, BatchTolerance);
            // Order matters: each batch vector is as near to its own text's single-call vector as to any other's (texts the
            // embedder treats as the same, like a paraphrase and its query, may tie).
            var own = Cosine(batch[i], singles[i]);
            for (var j = 0; j < texts.Length; j++)
            {
                own.ShouldBeGreaterThanOrEqualTo(Cosine(batch[i], singles[j]) - Tolerance, $"batch vector {i} is nearer to the single-call vector of text {j}");
            }
        }
    }

    [Test]
    public async Task Embed_WhenParaphrase_ShouldBeCloserThanUnrelated()
    {
        var query = await _embedder.EmbedQueryAsync(Query);
        var passages = await _embedder.EmbedPassagesAsync([Paraphrase, Unrelated]);

        Cosine(query, passages[0]).ShouldBeGreaterThan(Cosine(query, passages[1]));
    }

    protected static float Norm(float[] vector) => MathF.Sqrt(vector.Sum(x => x * x));

    protected static float Cosine(float[] a, float[] b) => a.Zip(b, (x, y) => x * y).Sum() / (Norm(a) * Norm(b));
}
