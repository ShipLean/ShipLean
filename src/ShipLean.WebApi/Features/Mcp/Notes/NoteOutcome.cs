namespace ShipLean.WebApi.Features.Mcp.Notes;

// Added is false when the note did not land, and Error says why.
public sealed record NoteOutcome(bool Added, string? Error);
