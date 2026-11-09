using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Asks;

// The question routes: any member answers or withdraws, not only the item's owner, so both minimums are Member. The URLs name
// the seeded asks' Story and its open questions.
public sealed class QuestionRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("POST", "api/work-items/{id:guid}/questions/{number:int}/answer", ProjectRole.Member, Body: """{"body":"x"}""",
            Url: $"api/work-items/{SeedIds.Asks.Item}/questions/1/answer"),
        new("POST", "api/work-items/{id:guid}/questions/{number:int}/withdraw", ProjectRole.Member,
            Url: $"api/work-items/{SeedIds.Asks.Item}/questions/2/withdraw"),
    ];
}
