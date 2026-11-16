using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;
using ShipLean.TestSupport.Plans;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Guide;

// planning_guide names a comment retro lesson by the day it was written, in the zone of the agent's user. The comment is stamped at
// 20:00 UTC on Sun 10 Mar 2024, which is Mon 11 Mar in Bangkok. Joel reads in Asia/Bangkok, Priya has no zone and reads in UTC.
[TestFixture]
public class PlanningGuideLessonsViewerZoneTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeRetroCommentAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        await using var connection = await OpenDatabaseAsync();
        await new CommentBuilder(keys.Ids[keys.Feature]).WithAuthor(SeedIds.Users.Joel)
            .WithBody("Retro · zone\n\n## What to change next time\n\nZone lesson.")
            .WithCreatedAt(new DateTimeOffset(2024, 3, 10, 20, 0, 0, TimeSpan.Zero)).InsertAsync(connection);
        await ViewerZoneArrange.SetAsync(Api, TestUser.Joel, ViewerZoneTestData.BangkokId);
    }

    private async Task<string> RetroLabelAsync(TestUser user)
    {
        var reply = await CallToolAsync(await TokenAsync(user, ReadOnly), "planning_guide", """{"project":"SHIP"}""");
        return reply.Structured().GetProperty("lessons").EnumerateArray()
            .Single(l => l.GetProperty("text").GetString() == "Zone lesson.").GetProperty("retro").GetString()!;
    }

    // Mutation: pass TimeZoneInfo.Utc instead of viewerZone.Zone to DisplayDate.DayLabel in ProjectLessons.
    [Test]
    public async Task PlanningGuide_WhenAgentsUserIsInBangkok_ShouldNameACommentLessonByTheBangkokDay()
    {
        (await RetroLabelAsync(TestUser.Joel)).ShouldBe("comment Mon 11 Mar");
    }

    [Test]
    public async Task PlanningGuide_WhenAgentsUserHasNoZone_ShouldNameACommentLessonByTheUtcDay()
    {
        (await RetroLabelAsync(TestUser.Priya)).ShouldBe("comment Sun 10 Mar");
    }
}
