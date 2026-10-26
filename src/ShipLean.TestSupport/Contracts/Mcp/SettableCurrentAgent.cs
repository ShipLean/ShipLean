using ShipLean.Contracts;

namespace ShipLean.TestSupport.Contracts.Mcp;

// The ICurrentAgent of a test: set who is acting (or None) before the call. Register it with ConfigureTestServices.
public sealed class SettableCurrentAgent : ICurrentAgent
{
    public string? Name { get; set; }

    public string? Host { get; set; }

    public bool Verified { get; set; }

    public void ActAs(string? name, string? host, bool verified)
    {
        Name = name;
        Host = host;
        Verified = verified;
    }

    public void Clear() => ActAs(null, null, false);
}
