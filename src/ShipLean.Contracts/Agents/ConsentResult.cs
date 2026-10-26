namespace ShipLean.Contracts.Agents;

// Where the browser goes next: the client's redirect URI with code, state and iss, or with error=access_denied.
public sealed record ConsentResult(string RedirectUrl);
