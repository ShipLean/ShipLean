using System.Text.Json.Nodes;
using ShipLean.Contracts.Criteria;
using ShipLean.UnitTest.Dtos.Boards;

namespace ShipLean.UnitTest.Dtos.Criteria;

[TestFixture]
public class CriteriaDtoTests
{
    [Test]
    public void CriteriaDto_WhenRoundTripped_ShouldKeepClauseErrorsAndTheDocument()
    {
        var document = JsonNode.Parse("""
            {
              "criteria": { "match": "all", "clauses": [ { "field": "colour", "op": "=", "value": "red" } ] },
              "errors": [ { "clauseIndex": 0, "field": "colour", "code": "unknownField", "message": "colour is not a field you can filter on." } ]
            }
            """)!;

        BoardDtoFixture.SurvivesRoundTrip<CriteriaDto>(document).ShouldBeTrue();
    }

    [Test]
    public void CriteriaDto_WhenNoCriteria_ShouldWriteNullNotOmit()
    {
        var document = JsonNode.Parse("""{ "criteria": null, "errors": [] }""")!;

        BoardDtoFixture.SurvivesRoundTrip<CriteriaDto>(document).ShouldBeTrue();
    }
}
