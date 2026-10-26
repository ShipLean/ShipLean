using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Links;

// The link routes. Any member lists, previews, adds, edits, archives and refreshes links, so every minimum is Member. They
// name SHIP-101 and its two seeded links: the edit row the pull request, the archive row the page link that nothing else uses.
public sealed class LinkCardsRows : IAccessSweepRows
{
    private static readonly string Item = $"api/work-items/{SeedIds.WorkItems.Ship101}";

    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/work-items/{id:guid}/link-cards", ProjectRole.Member, Url: $"{Item}/link-cards"),
        new("POST", "api/work-items/{id:guid}/link-cards/preview", ProjectRole.Member, Body: """{"url":"https://docs.example/preview"}""", Url: $"{Item}/link-cards/preview"),
        new("POST", "api/work-items/{id:guid}/link-cards", ProjectRole.Member, Body: """{"url":"https://docs.example/sweep"}""", Url: $"{Item}/link-cards"),
        new("POST", "api/work-items/{id:guid}/work-item-links", ProjectRole.Member, Body: """{"targetKey":"SHIP-90","type":"related"}""", Url: $"{Item}/work-item-links"),
        new("POST", "api/work-items/{id:guid}/link-cards/refresh", ProjectRole.Member, Url: $"{Item}/link-cards/refresh"),
        new("PATCH", "api/link-cards/{id:guid}", ProjectRole.Member, Body: """{"note":"Checked in the sweep"}""", Url: $"api/link-cards/{SeedIds.Links.Ship101Pull}"),
        new("POST", "api/link-cards/{id:guid}/archive", ProjectRole.Member, Url: $"api/link-cards/{SeedIds.Links.Ship101PageToArchive}/archive"),
    ];
}
