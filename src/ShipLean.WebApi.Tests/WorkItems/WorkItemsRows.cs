using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.WorkItems;

// The work item routes. The writes use stale revisions or a blank title, so the allowed leg gets a 409 or a 400 and writes nothing.
public sealed class WorkItemsRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/projects/{id:guid}/work-items", ProjectRole.Member),
        new("GET", "api/projects/{id:guid}/work-items/by-key/{itemKey}", ProjectRole.Member, Url: "api/projects/" + SeedIds.Projects.Ship + "/work-items/by-key/SHIP-90"),
        new("GET", "api/projects/{id:guid}/tags", ProjectRole.Member),
        new("GET", "api/work-items/{id:guid}", ProjectRole.Member, Url: $"api/work-items/{SeedIds.WorkItems.Ship90}"),
        new("POST", "api/work-items", ProjectRole.Member, Body: $$"""{"projectId":"{{SeedIds.Projects.Ship}}","type":"story","title":"","parentId":null,"sprintId":null,"changes":null}"""),
        new("PATCH", "api/work-items/{id:guid}", ProjectRole.Member, Body: """{"expectedRevision":999,"changes":{}}""", Url: $"api/work-items/{SeedIds.WorkItems.Ship91}"),
        new("POST", "api/work-items/{id:guid}/reorder", ProjectRole.Member, Body: """{"expectedRevision":999,"beforeItemId":null}""", Url: $"api/work-items/{SeedIds.WorkItems.Ship92}/reorder"),
        new("POST", "api/work-items/{id:guid}/remove", ProjectRole.Member, Body: """{"expectedRevision":999}""", Url: $"api/work-items/{SeedIds.WorkItems.Ship93}/remove"),
    ];
}
