namespace ShipLean.AcceptanceTests.Lanes.Touch;

// How the route sweep opens one screen: its seeded URL (given the Markdown attachment the sweep uploads to SHIP-101,
// which only workItemAttachment uses), the test ids that say its data has rendered (one per
// query the screen waits on; the first match of each counts), and whether it is opened signed out.
public sealed record TouchRoute(Func<Guid, string> Url, IReadOnlyList<string> ReadyTestIds, bool SignedOut = false);
