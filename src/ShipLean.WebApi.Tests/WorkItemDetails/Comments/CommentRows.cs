using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Comments;

// The comment routes. Any member comments, so every minimum is Member; editing and archiving add the author rule beyond the sweep.
// The two comment-id routes name the seeded comments on SHIP-90.
public sealed class CommentRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/work-items/{id:guid}/comments", ProjectRole.Member, Url: $"api/work-items/{SeedIds.WorkItems.Ship90}/comments"),
        new("POST", "api/work-items/{id:guid}/comments", ProjectRole.Member, Body: """{"body":""}""", Url: $"api/work-items/{SeedIds.WorkItems.Ship90}/comments"),
        new("PATCH", "api/comments/{id:guid}", ProjectRole.Member, Body: """{"body":""}""", Url: $"api/comments/{SeedIds.Comments.Ship90Note}"),
        new("POST", "api/comments/{id:guid}/supersede", ProjectRole.Member, Url: $"api/comments/{SeedIds.Comments.Ship90Note}/supersede"),
        new("POST", "api/comments/{id:guid}/archive", ProjectRole.Member, Url: $"api/comments/{SeedIds.Comments.Ship90NoteToArchive}/archive"),
    ];
}
