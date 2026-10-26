using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Embedding;

// Lexical only: the off switch and the result of a model that would not load. Callers check Status before embedding.
public sealed class NoEmbedder(string? reason = null) : IEmbedder
{
    public EmbeddingModelId? ModelId => null;

    public int Dimensions => 0;

    public EmbedderStatus Status { get; } = new(EmbedderMode.Lexical, reason);

    public Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Embeddings are not available; check IEmbedder.Status first.");

    public Task<IReadOnlyList<float[]>> EmbedPassagesAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
        => throw new InvalidOperationException("Embeddings are not available; check IEmbedder.Status first.");
}
