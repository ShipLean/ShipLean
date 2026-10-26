namespace ShipLean.Contracts.Search;

// Name and revision of an embedding model. A stored vector is stale when the model that wrote it differs from the loaded one.
public sealed record EmbeddingModelId(string Name, string Revision)
{
    public override string ToString() => $"{Name}@{Revision}";
}
