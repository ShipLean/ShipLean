using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Asks;

// The decision routes. Any member answers, whoever owns the item, so every minimum is Member. They name the seeded decisions on
// SHIP-124 (Tom's Story, so tests acting as the other users never wait on them): D1 confirm, D2 change, D3 withdraw, D4 discuss.
public sealed class DecisionRows : IAccessSweepRows
{
    private static readonly string Base = $"api/work-items/{SeedIds.Asks.Item}/decisions";

    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("POST", "api/work-items/{id:guid}/decisions/confirm", ProjectRole.Member, Body: """{"numbers":[1]}""", Url: $"{Base}/confirm"),
        new("POST", "api/work-items/{id:guid}/decisions/{number:int}/change", ProjectRole.Member, Body: """{"kind":"own","text":"x"}""", Url: $"{Base}/2/change"),
        new("POST", "api/work-items/{id:guid}/decisions/{number:int}/withdraw", ProjectRole.Member, Url: $"{Base}/3/withdraw"),
        new("POST", "api/work-items/{id:guid}/decisions/{number:int}/discuss", ProjectRole.Member, Body: """{"body":"x"}""", Url: $"{Base}/4/discuss"),
    ];
}
