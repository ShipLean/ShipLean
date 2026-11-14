using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.TaskChanges.Http;

// The eight Changes routes. Any member reads, comments and reviews, so every minimum is Member. Each row names SHIP-101; a code comment id
// is checked against that item, so the comment rows carry an id no comment has.
public sealed class TaskChangesRows : IAccessSweepRows
{
    private static readonly string Item = $"api/work-items/{SeedIds.WorkItems.Ship101}";
    private const string Comment = "00000000-0000-4000-8000-0000000000c1";
    private const string Sha = "a81d4e0c3f5b79d2e6a1c8b04f7d93e5a2b6c1d8";

    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/work-items/{id:guid}/commits", ProjectRole.Member, Url: $"{Item}/commits"),
        new("GET", "api/work-items/{id:guid}/changes", ProjectRole.Member, Url: $"{Item}/changes?commit=all"),
        new("POST", "api/work-items/{id:guid}/commits", ProjectRole.Member, Body: $$"""{"repo":"acme/storefront","sha":"{{Sha}}"}""", Url: $"{Item}/commits"),
        new(
            "POST", "api/work-items/{id:guid}/code-comments", ProjectRole.Member,
            Body: $$"""{"repo":"acme/storefront","sha":"{{Sha}}","path":"src/a.ts","side":"new","line":1,"startLine":null,"lineText":"x","body":"Looks wrong"}""",
            Url: $"{Item}/code-comments"),
        new("POST", "api/work-items/{id:guid}/code-comments/{commentId:guid}/replies", ProjectRole.Member, Body: """{"body":"Fixed"}""", Url: $"{Item}/code-comments/{Comment}/replies"),
        new("POST", "api/work-items/{id:guid}/code-comments/{commentId:guid}/resolve", ProjectRole.Member, Body: """{"resolved":true}""", Url: $"{Item}/code-comments/{Comment}/resolve"),
        new("POST", "api/work-items/{id:guid}/changes/approve", ProjectRole.Member, Body: $$"""{"note":null,"headCommitId":"{{Comment}}"}""", Url: $"{Item}/changes/approve"),
        new("POST", "api/work-items/{id:guid}/changes/request", ProjectRole.Member, Body: """{"note":null,"keepAssignee":false}""", Url: $"{Item}/changes/request"),
    ];
}
