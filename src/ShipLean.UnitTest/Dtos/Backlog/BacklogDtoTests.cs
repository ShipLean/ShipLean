using System.Text.Json;
using ShipLean.Contracts.Backlog;
using ShipLean.UnitTest.Dtos.Boards;

namespace ShipLean.UnitTest.Dtos.Backlog;

// Each backlog write and review shape round-trips through the API's serializer unchanged: the fixture is the wire shape.
[TestFixture]
public class BacklogDtoTests
{
    [Test]
    public void BulkWriteRequest_WhenRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<BulkWriteRequest>(BoardDtoFixture.Read("backlog", "bulk-write-request.json")).ShouldBeTrue();
    }

    [Test]
    public void BulkWriteResult_WhenRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<BulkWriteResult>(BoardDtoFixture.Read("backlog", "bulk-write-result.json")).ShouldBeTrue();
    }

    [Test]
    public void BacklogAddRequest_WhenRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<BacklogAddRequest>(BoardDtoFixture.Read("backlog", "backlog-add-request.json")).ShouldBeTrue();
    }

    [Test]
    public void BacklogAddResult_WhenRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<BacklogAddResult>(BoardDtoFixture.Read("backlog", "backlog-add-result.json")).ShouldBeTrue();
    }

    [Test]
    public void DuplicateCheckRequest_WhenRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<DuplicateCheckRequest>(BoardDtoFixture.Read("backlog", "duplicate-check-request.json")).ShouldBeTrue();
    }

    [Test]
    public void DuplicateCheckResult_WhenRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<DuplicateCheckResult>(BoardDtoFixture.Read("backlog", "duplicate-check-result.json")).ShouldBeTrue();
    }

    [Test]
    public void BacklogReviewDto_WhenRoundTripped_ShouldMatchTheFixture()
    {
        BoardDtoFixture.SurvivesRoundTrip<BacklogReviewDto>(BoardDtoFixture.Read("backlog", "backlog-review.json")).ShouldBeTrue();
    }

    [Test]
    public void BacklogReviewFixture_ShouldCarryTheFiveChecksInOrder()
    {
        var review = BoardDtoFixture.Read("backlog", "backlog-review.json").Deserialize<BacklogReviewDto>(BoardDtoFixture.Options).ShouldNotBeNull();

        review.Checks.Select(c => c.Id).ShouldBe(BacklogCheckIds.All);
    }
}
