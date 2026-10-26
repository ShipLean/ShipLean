namespace ShipLean.Contracts.Agents;

// What the consent page shows about a pending authorize request. ClientHost is the verified host of a metadata-document
// client, null when the client is unverified. RedirectHost is where the person is sent back to. LocalhostOnly: every
// registered redirect of the client is a loopback address, which the page warns about.
public sealed record ConsentRequestDto(
    Guid RequestId,
    string ClientName,
    string? ClientHost,
    bool Verified,
    string RedirectHost,
    IReadOnlyList<string> RequestedScopes,
    bool LocalhostOnly);
