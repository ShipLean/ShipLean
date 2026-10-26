using System.Text;
using System.Text.RegularExpressions;
using ShipLean.Contracts.Search;

namespace ShipLean.TestSupport.Search;

// A deterministic stand-in for the real model: a hashed bag of words over 128 dimensions, with a small synonym table so
// paraphrases land together. Words are lower-cased, "sign-in" style spellings are folded to "login", stop words are dropped,
// each remaining word is replaced by its concept (below), and each concept adds +1 or -1 to one dimension chosen by an FNV-1a
// hash. The vector is L2-normalised. Text with no content words (empty, or only stop words) maps to a fixed unit vector, so
// every output is a unit vector. Query and passage embeddings are identical (the real embedder's prefixes are not modelled).
//
// Concepts (every word in a row counts as the first): login = login, signin, logon, authenticate, authentication, password;
// fault = broken, fails, failing, failed, failure, crash, crashes, crashing, error, errors, bug, bugs, defect;
// slow = slow, sluggish, latency, lag, laggy; payment = payment, payments, pay, checkout, billing, invoice;
// delete = delete, remove, removal, erase; login-style spellings are folded first: sign in, sign-in, log in, log-in.
// Two texts that use the same concepts therefore embed to the same vector, and texts with no shared concept sit near 0.
//
// Settable ModelId lets a test stand in for a model swap. The call counters let a test assert how often the indexer or the
// ranker embedded.
public sealed class FakeEmbedder : IEmbedder
{
    public const int VectorSize = 128;

    private static readonly Regex SignIn = new(@"\b(sign|log)[\s-]?in\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex Word = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "the", "and", "or", "of", "to", "in", "on", "is", "are", "was", "be", "it", "for", "with", "when", "my", "our", "i", "we",
    ];

    private static readonly Dictionary<string, string> Concepts = BuildConcepts();

    private int _queryCalls;
    private int _passageCalls;
    private int _passageTexts;

    public EmbeddingModelId? ModelId { get; set; } = new("fake-embedder", "1");

    public int Dimensions => VectorSize;

    public EmbedderStatus Status { get; } = new(EmbedderMode.Semantic);

    public int QueryCalls => Volatile.Read(ref _queryCalls);

    // Calls of EmbedPassagesAsync, and the texts across them.
    public int PassageCalls => Volatile.Read(ref _passageCalls);

    public int PassageTexts => Volatile.Read(ref _passageTexts);

    public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _queryCalls);
        return Task.FromResult(Embed(text));
    }

    public Task<IReadOnlyList<float[]>> EmbedPassagesAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _passageCalls);
        Interlocked.Add(ref _passageTexts, texts.Count);
        return Task.FromResult<IReadOnlyList<float[]>>(texts.Select(Embed).ToList());
    }

    private static float[] Embed(string text)
    {
        var vector = new float[VectorSize];
        foreach (var match in Word.Matches(SignIn.Replace(text.ToLowerInvariant(), "login")).Select(m => m.Value))
        {
            if (StopWords.Contains(match))
            {
                continue;
            }
            var concept = Concepts.GetValueOrDefault(match, match);
            var hash = Fnv(concept);
            vector[hash % VectorSize] += (hash >> 16 & 1) == 0 ? 1f : -1f;
        }
        var norm = MathF.Sqrt(vector.Sum(x => x * x));
        if (norm == 0f)
        {
            vector[0] = 1f;
            return vector;
        }
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= norm;
        }
        return vector;
    }

    private static uint Fnv(string value)
    {
        var hash = 2166136261u;
        foreach (var b in Encoding.UTF8.GetBytes(value))
        {
            hash = (hash ^ b) * 16777619u;
        }
        return hash;
    }

    private static Dictionary<string, string> BuildConcepts()
    {
        var concepts = new Dictionary<string, string>();
        foreach (var group in new[]
        {
            new[] { "login", "signin", "logon", "authenticate", "authentication", "password" },
            ["fault", "broken", "fails", "failing", "failed", "failure", "crash", "crashes", "crashing", "error", "errors", "bug", "bugs", "defect"],
            ["slow", "sluggish", "latency", "lag", "laggy"],
            ["payment", "payments", "pay", "checkout", "billing", "invoice"],
            ["delete", "remove", "removal", "erase"],
        })
        {
            foreach (var word in group)
            {
                concepts[word] = group[0];
            }
        }
        return concepts;
    }
}
