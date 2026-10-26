namespace ShipLean.Contracts.Links;

// Where a card's title came from: read from the provider's API (Live), the page's own title (Fetched), typed by a person
// (User), or nothing better than the address (Url).
public enum LinkTitleSource
{
    Live,
    Fetched,
    User,
    Url,
}
