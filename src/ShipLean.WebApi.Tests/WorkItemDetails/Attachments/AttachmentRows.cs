using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// The attachment and history routes. Any member lists, uploads, downloads and archives attachments and reads history, so
// every minimum is Member. The two attachment-id routes name the seeded attachments on SHIP-90.
public sealed class AttachmentRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/work-items/{id:guid}/attachments", ProjectRole.Member, Url: $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments"),
        new("POST", "api/work-items/{id:guid}/attachments", ProjectRole.Member, Body: "{}", Url: $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments"),
        new("GET", "api/work-items/{id:guid}/history", ProjectRole.Member, Url: $"api/work-items/{SeedIds.WorkItems.Ship90}/history"),
        new("POST", "api/work-items/{id:guid}/attachments/check", ProjectRole.Member, Body: """{"files":[]}""", Url: $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments/check"),
        new("GET", "api/attachments/{id:guid}/markdown", ProjectRole.Member, Url: $"api/attachments/{SeedIds.Attachments.Ship90Scope}/markdown"),
        new("GET", "api/attachments/{id:guid}/content", ProjectRole.Member, Url: $"api/attachments/{SeedIds.Attachments.Ship90Scope}/content"),
        new("POST", "api/attachments/{id:guid}/archive", ProjectRole.Member, Url: $"api/attachments/{SeedIds.Attachments.Ship90ScopeToArchive}/archive"),
    ];
}
