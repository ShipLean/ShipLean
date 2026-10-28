using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Planning;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Guide;

// planning_guide: the guide or one part of it, and (with a project that has no retros) no lessons. Joel is a member of SHIP and not of PART.
[TestFixture]
public class PlanningGuideTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [Test]
    public async Task PlanningGuide_NoPart_ShouldReturnGuide()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "planning_guide");

        reply.IsToolError().ShouldBeFalse();
        reply.Structured().GetProperty("part").GetString().ShouldBe("guide");
        reply.Structured().GetProperty("markdown").GetString().ShouldBe(PlanGuide.Read(PlanGuidePart.Guide));
        reply.Structured().GetProperty("parts").EnumerateArray().Select(p => p.GetString()).ShouldBe(Enum.GetValues<PlanGuidePart>().Select(p => p.WireName()));
    }

    [Test]
    public async Task PlanningGuide_WhenClientReadsOnlyText_ShouldGiveTheSummaryThenTheMarkdownThenTheJson()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "planning_guide", """{"part":"work"}""");

        var texts = reply.Texts();
        texts.Count.ShouldBe(3);
        texts[0].ShouldBe("The work part of the planning guide; the Markdown follows.");
        texts[1].ShouldBe(PlanGuide.Read(PlanGuidePart.Work));
        reply.TextJson().GetProperty("markdown").GetString().ShouldBe(texts[1]);
    }

    [Test]
    public async Task PlanningGuide_EachPart_ShouldReturnThatFile()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        foreach (var part in Enum.GetValues<PlanGuidePart>())
        {
            var reply = await CallToolAsync(token, "planning_guide", $$"""{"part":"{{part.WireName()}}"}""");

            reply.IsToolError().ShouldBeFalse(part.ToString());
            reply.Structured().GetProperty("part").GetString().ShouldBe(part.WireName());
            reply.Structured().GetProperty("markdown").GetString().ShouldBe(PlanGuide.Read(part), part.ToString());
        }
    }

    [Test]
    public async Task PlanningGuide_UnknownPart_ShouldListParts()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "planning_guide", """{"part":"nope"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe("validation");
        error.GetProperty("field").GetString().ShouldBe("part");
        error.GetProperty("message").GetString()!.ShouldContain("plan_reviewer");
    }

    [Test]
    public async Task PlanningGuide_ProjectWithoutRetros_ShouldHaveNoLessons()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "planning_guide", """{"project":"SHIP"}""");

        reply.IsToolError().ShouldBeFalse();
        reply.Structured().GetProperty("lessons").GetArrayLength().ShouldBe(0);
    }

    [Test]
    public async Task PlanningGuide_ProjectNotMember_ShouldBeNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "planning_guide", """{"project":"PART"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("not_found");
    }
}
