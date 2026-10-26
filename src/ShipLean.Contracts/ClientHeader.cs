namespace ShipLean.Contracts;

// The request header every browser tab sends with a random per-tab id. The server stamps it onto the events a
// request causes, so a tab can ignore its own events when they come back over the real-time connection.
public static class ClientHeader
{
    public const string Name = "X-ShipLean-Client";
}
