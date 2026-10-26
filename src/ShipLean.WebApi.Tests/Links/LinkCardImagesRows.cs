using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Links;

// The stored preview image and favicon of a link card: any member reads them. Both rows name SHIP-101's seeded pull request
// link, whose image and favicon blobs the seeded blob store holds.
public sealed class LinkCardImagesRows : IAccessSweepRows
{
    private static readonly string Link = $"api/link-cards/{SeedIds.Links.Ship101Pull}";

    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/link-cards/{id:guid}/image", ProjectRole.Member, Url: $"{Link}/image"),
        new("GET", "api/link-cards/{id:guid}/favicon", ProjectRole.Member, Url: $"{Link}/favicon"),
    ];
}
