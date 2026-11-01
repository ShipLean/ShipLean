namespace ShipLean.LocalHost;

// What the named database holds when the host starts: the seed rebuilt, what was there kept, or the schema alone.
public enum LocalHostData
{
    Fresh,
    Keep,
    Empty,
}
