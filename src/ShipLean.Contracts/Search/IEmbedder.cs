namespace ShipLean.Contracts.Search;

// Turns text into L2-normalised vectors. The implementation adds the model's query and passage prefixes.
// Callers check Status first: a lexical embedder (NoEmbedder) throws from both embed methods.
public interface IEmbedder
{
    // Null when Status is lexical and no model is loaded.
    EmbeddingModelId? ModelId { get; }

    int Dimensions { get; }

    EmbedderStatus Status { get; }

    Task<float[]> EmbedQueryAsync(string text, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<float[]>> EmbedPassagesAsync(IReadOnlyList<string> texts, CancellationToken cancellationToken = default);
}
