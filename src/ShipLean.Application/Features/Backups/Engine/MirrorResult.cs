namespace ShipLean.Application.Features.Backups.Engine;

// What one mirror pass copied: how many blobs, and how many bytes it uploaded for them.
public sealed record MirrorResult(int Copied, long Bytes);
