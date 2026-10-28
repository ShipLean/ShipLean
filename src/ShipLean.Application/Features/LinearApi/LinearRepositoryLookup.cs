namespace ShipLean.Application.Features.LinearApi;

// Found is false when the item or session is not the connection's. Repository is null when the stored URL does not parse.
public sealed record LinearRepositoryLookup(bool Found, LinearRepository? Repository)
{
    public static LinearRepositoryLookup NotFound { get; } = new(false, null);
}
