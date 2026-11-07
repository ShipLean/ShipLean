namespace ShipLean.AcceptanceTests.Lanes.Touch;

// How the route sweep opens one screen: its seeded URL (given the Markdown attachment the sweep uploads to SHIP-101,
// which only workItemAttachment uses), the test id that says the page is ready, and whether it is opened signed out.
public sealed record TouchRoute(Func<Guid, string> Url, string ReadyTestId, bool SignedOut = false);
