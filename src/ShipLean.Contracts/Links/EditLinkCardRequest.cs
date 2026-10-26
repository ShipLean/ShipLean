namespace ShipLean.Contracts.Links;

// PATCH api/link-cards/{id}. A field left out (null) is unchanged; an empty string clears it (a cleared title falls back to
// the fetched one, then the address).
public sealed record EditLinkCardRequest(string? Title, string? Note);
