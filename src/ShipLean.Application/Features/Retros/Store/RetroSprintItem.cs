namespace ShipLean.Application.Features.Retros.Store;

// A story-level item that finished in a sprint or was rolled over out of it.
public sealed record RetroSprintItem(Guid ItemId, bool Finished, bool RolledOver);
