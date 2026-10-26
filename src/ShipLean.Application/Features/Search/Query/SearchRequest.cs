namespace ShipLean.Application.Features.Search.Query;

// The query string of GET /api/search. Types is a comma list of type names.
public sealed record SearchRequest(string? Q, string? Project, string? Types, int Limit = SearchRequest.DefaultLimit, bool Closed = false, bool Prefix = false)
{
    public const int DefaultLimit = 10;

    public const int MaxLimit = 25;
}
