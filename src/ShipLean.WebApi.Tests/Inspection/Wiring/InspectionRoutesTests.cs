using ShipLean.WebApi.Tests.Taskboard;
using System.Net;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Inspection.Wiring;

// K5 reads over FakeTraceSource: the pinned shapes, not_found for a non-member, and not_found for a run that is not linked to {key}.
[TestFixture]
public class InspectionRoutesTests : InspectionApiFixture
{
    [OneTimeSetUp]
    public async Task EnableProject()
    {
        using var response = await TurnOnAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Section_WhenRunLinkedToItem_ShouldListIt()
    {
        var section = await Api.GetAsync<AgentRunsSectionDto>(RunsUrl(Item));

        section.StoreReachable.ShouldBeTrue();
        section.Rows.Select(r => r.RunId).ShouldBe([RunId]);
        section.Rows[0].Agent.ShouldBe("claude-code");
    }

    [Test]
    public async Task RunPageAndRow_WhenRunLinkedToItem_ShouldReturnTheirShapes()
    {
        var page = await Api.GetAsync<RunPageDto>($"{RunsUrl(Item)}/{RunId}");
        var row = page.Turns[0].Rows[0];
        var detail = await Api.GetAsync<RunRowDetailDto>($"{RunsUrl(Item)}/{RunId}/rows/{row.Id}");

        page.Header.Agent.ShouldBe("claude-code");
        detail.Name.ShouldBe(row.Name);
    }

    [Test]
    public async Task Section_WhenNonMember_ShouldBeNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).GetAsync(RunsUrl(Item));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Run_WhenNotLinkedToTheItemInTheUrl_ShouldBeNotFoundForAMember()
    {
        using var run = await Api.GetAsync($"{RunsUrl(OtherItem)}/{RunId}");
        using var row = await Api.GetAsync($"{RunsUrl(OtherItem)}/{RunId}/rows/anything");

        run.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        row.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
