using System.Globalization;
using Microsoft.Extensions.Configuration;

namespace ShipLean.Application.Features.Search;

// Search settings. Built from configuration when first resolved (SearchServices), so test hosts that override
// configuration get their own. The defaults are the issue's; the tuned values replace them later.
public sealed record SearchOptions
{
    public const string EmbeddingsKey = "SHIPLEAN_EMBEDDINGS";
    public const string ModelPathKey = "SHIPLEAN_EMBEDDINGS_MODEL_PATH";

    // false when SHIPLEAN_EMBEDDINGS=off: lexical only.
    public bool Embeddings { get; init; } = true;

    // Null when SHIPLEAN_EMBEDDINGS_MODEL_PATH is unset (lexical, with a reason).
    public string? ModelPath { get; init; }

    public int ChunkChars { get; init; } = 1200;

    public int ChunkOverlap { get; init; } = 150;

    // Cosine floor of the semantic leg. It belongs to one model and pooling pair (e5-small int8, mean pooling): measure it again for any other.
    public double Cutoff { get; init; } = 0.84;

    public int TopPerList { get; init; } = 100;

    public int RrfK { get; init; } = 60;

    public int Batch { get; init; } = 32;

    // ONNX IntraOpNumThreads.
    public int Threads { get; init; } = 1;

    public int FirstPassSeconds { get; init; } = 20;

    public int IntervalSeconds { get; init; } = 60;

    public static SearchOptions From(IConfiguration configuration)
    {
        var defaults = new SearchOptions();
        var options = new SearchOptions
        {
            Embeddings = Switch(configuration, EmbeddingsKey, defaults.Embeddings),
            ModelPath = string.IsNullOrWhiteSpace(configuration[ModelPathKey]) ? null : configuration[ModelPathKey],
            ChunkChars = Number(configuration, "SHIPLEAN_SEARCH_CHUNK_CHARS", defaults.ChunkChars),
            ChunkOverlap = Number(configuration, "SHIPLEAN_SEARCH_CHUNK_OVERLAP", defaults.ChunkOverlap),
            Cutoff = Decimal(configuration, "SHIPLEAN_SEARCH_CUTOFF", defaults.Cutoff),
            TopPerList = Number(configuration, "SHIPLEAN_SEARCH_TOP_PER_LIST", defaults.TopPerList),
            RrfK = Number(configuration, "SHIPLEAN_SEARCH_RRF_K", defaults.RrfK),
            Batch = Number(configuration, "SHIPLEAN_SEARCH_BATCH", defaults.Batch),
            Threads = Number(configuration, "SHIPLEAN_SEARCH_THREADS", defaults.Threads),
            FirstPassSeconds = Number(configuration, "SHIPLEAN_SEARCH_FIRST_PASS_SECONDS", defaults.FirstPassSeconds),
            IntervalSeconds = Number(configuration, "SHIPLEAN_SEARCH_INTERVAL_SECONDS", defaults.IntervalSeconds),
        };
        return options.ChunkOverlap >= options.ChunkChars
            ? throw new InvalidOperationException($"SHIPLEAN_SEARCH_CHUNK_OVERLAP ({options.ChunkOverlap}) must be less than SHIPLEAN_SEARCH_CHUNK_CHARS ({options.ChunkChars}).")
            : options;
    }

    private static bool Switch(IConfiguration configuration, string key, bool fallback)
        => configuration[key]?.Trim().ToLowerInvariant() switch
        {
            null or "" => fallback,
            "on" => true,
            "off" => false,
            var other => throw new InvalidOperationException($"{key} must be on or off, not \"{other}\"."),
        };

    private static int Number(IConfiguration configuration, string key, int fallback)
    {
        var text = configuration[key];
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) && value > 0
            ? value
            : throw new InvalidOperationException($"{key} must be a positive whole number, not \"{text}\".");
    }

    private static double Decimal(IConfiguration configuration, string key, double fallback)
    {
        var text = configuration[key];
        if (string.IsNullOrWhiteSpace(text))
        {
            return fallback;
        }

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && value is >= 0 and <= 1
            ? value
            : throw new InvalidOperationException($"{key} must be a number from 0 to 1, not \"{text}\".");
    }
}
