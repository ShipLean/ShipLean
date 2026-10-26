namespace ShipLean.Contracts;

// Where the current request came from: the value of the ClientHeader, or null when the request carries none (a script,
// a test client) or there is no request at all.
public interface IRequestOrigin
{
    string? ClientId { get; }
}
