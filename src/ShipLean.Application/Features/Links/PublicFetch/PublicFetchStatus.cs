namespace ShipLean.Application.Features.Links.PublicFetch;

public enum PublicFetchStatus
{
    Ok,

    // Timed out, refused the connection, answered an error, or redirected too often or off http/https.
    Unreachable,

    // The host (or a redirect's host) resolves to an address ShipLean refuses to call.
    Private,
}
