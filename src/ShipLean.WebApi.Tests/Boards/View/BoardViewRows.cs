using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Boards.View;

// The board read, move and children routes. The move sends SHIP-165 to the column it already sits in, so the allowed leg writes nothing.
public sealed class BoardViewRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/boards/{id:guid}", ProjectRole.Member, Url: $"api/boards/{SeedIds.Boards.ShipStories}"),
        new("POST", "api/boards/{id:guid}/moves", ProjectRole.Member, Body: MoveBody(), Url: $"api/boards/{SeedIds.Boards.ShipStories}/moves"),
        new("GET", "api/work-items/{id:guid}/children", ProjectRole.Member, Url: $"api/work-items/{SeedIds.WorkItems.Ship101}/children"),
    ];

    private static string MoveBody()
        => $$"""{"itemId":"{{SeedIds.WorkItems.Ship165}}","expectedRevision":1,"columnId":"col-new","done":false}""";
}
