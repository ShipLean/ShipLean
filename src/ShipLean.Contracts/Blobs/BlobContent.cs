namespace ShipLean.Contracts.Blobs;

// A stored blob. The caller disposes the stream.
public sealed record BlobContent(Stream Content, string ContentType);
