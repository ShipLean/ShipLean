namespace ShipLean.Contracts.Links;

// POST api/work-items/{id}/link-cards. A title or note left out stays empty.
public sealed record AddLinkCardRequest(string Url, string? Title, string? Note);
