using System.Text.Json;
using ShipLean.AcceptanceTests.Pages.Backlog;
using ShipLean.TestSupport;

namespace ShipLean.AcceptanceTests.Lanes.Backlog.Pull;

// The Backlog rows of the seeded Storefront project (SHIP) that the Pull and group-drag journeys use, in rank order.
internal static class PullKeys
{
    public const string Sprint25 = "00000004-0000-4000-8001-000000000025";


    // The body of a bulk write: the item ids it carries, in order.
    public static IReadOnlyList<JsonElement> ItemsOf(string? postData) =>
        [.. JsonDocument.Parse(postData!).RootElement.GetProperty("items").EnumerateArray().Select(e => e.Clone())];

}
