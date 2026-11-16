using System.Globalization;
using ShipLean.Application.Features.TaskChanges.Comments;
using ShipLean.Contracts.TaskChanges;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Identity;

namespace ShipLean.UnitTest.TaskChanges;

// A code comment's time label (and its replies') is read in the viewer's zone, so a comment left late in the evening UTC
// reads as the next day for a viewer in Asia/Bangkok.
[TestFixture]
public class CodeCommentLabelZoneTests
{
    private static CodeComment Comment(DateTimeOffset at, params CodeComment[] replies) =>
        new(Guid.NewGuid(), null, null, null, null, null, null, null, null, "body", "<p>body</p>", "Joel", false, at, null, null, null, replies);

    [Test]
    public void From_WhenViewerIsInBangkok_ShouldLabelTheCommentAndItsReplyInThatZone()
    {
        var now = TestDates.Today;
        var comment = Comment(ViewerZoneTestData.LateEveningUtc, Comment(ViewerZoneTestData.LateEveningUtc));

        var dto = CodeCommentDtos.From(comment, now, ViewerZoneTestData.Bangkok);

        // An earlier year reads "d MMM yyyy" on the zone's calendar day; worked out by hand at +07:00, not through DisplayDate.
        var bangkokDay = ViewerZoneTestData.LateEveningUtc.ToOffset(TimeSpan.FromHours(7));
        dto.CreatedAtLabel.ShouldBe(bangkokDay.ToString("d MMM yyyy", CultureInfo.InvariantCulture));
        dto.CreatedAtLabel.ShouldNotBe(ViewerZoneTestData.LateEveningUtc.ToString("d MMM yyyy", CultureInfo.InvariantCulture));
        dto.Replies[0].CreatedAtLabel.ShouldBe(dto.CreatedAtLabel);
    }
}
