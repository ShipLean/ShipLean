using ShipLean.WebApi.Tests.Taskboard;
using System.Net;
using ShipLean.Contracts.Inspection;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Inspection.Wiring;

// The project starts off. Settings and agent setup still answer; an admin turns it on, a plain member cannot.
[TestFixture]
public class InspectionProjectSettingsTests : InspectionApiFixture
{
    [Test]
    [Order(1)]
    public async Task Settings_WhenProjectOff_ShouldAnswerForAnAdmin()
    {
        var settings = await Api.GetAsync<InspectionProjectSettingsDto>(SettingsUrl);
        using var setup = await Api.GetAsync($"api/inspection/projects/{Project}/agent-setup?agent=claude-code&item={Item}");
        using var runs = await Api.GetAsync(RunsUrl(Item));

        settings.Enabled.ShouldBeFalse();
        setup.StatusCode.ShouldBe(HttpStatusCode.OK);
        runs.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    [Order(2)]
    public async Task Put_WhenMember_ShouldBeForbidden()
    {
        using var response = await Api.ActingAs(TestUser.Priya).PutJsonAsync(SettingsUrl, new InspectionProjectSettingsDto(true, true, null, null, "tempoLoki"));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Test]
    [Order(3)]
    public async Task Put_WhenAdmin_ShouldRoundTripEveryField()
    {
        var sent = new InspectionProjectSettingsDto(true, true, "tenant-a", "{service_name=\"x\"}", "tempoLoki");

        using var put = await Api.PutJsonAsync(SettingsUrl, sent);
        var read = await Api.GetAsync<InspectionProjectSettingsDto>(SettingsUrl);

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        read.ShouldBe(sent);
    }
}
