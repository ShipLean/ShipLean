namespace ShipLean.Contracts;

// The request header the SPA's API client sends on every request with the width class of the window at send time (the
// SPA's VIEWPORT_HEADER in api/client.ts). The server stamps it onto the usage events of a web request (UsageViewports).
public static class ViewportHeader
{
    public const string Name = "X-ShipLean-Viewport";
}
