using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Demos.Runs;
using ShipLean.Contracts.Demos;
using ShipLean.Core;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Identity;
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.Demos;

// The Demo read dates the request and the upload link in the viewer's zone. One demo requested at ViewerZoneTestData.LateEveningUtc
// (20:00 UTC, the next day in Bangkok); Joel reads in Asia/Bangkok, Priya has no zone and reads in UTC. Time is never frozen:
// expected labels are written out from the instants.
[TestFixture]
public class SprintDemoViewerZoneTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private Guid _sprintId;

    [OneTimeSetUp]
    public async Task ArrangeDemoAsync()
    {
        await using var database = await OpenDatabaseAsync();
        var project = await AgentDispatchArrange.CreateAsync(database, Api.ActingAs(TestUser.Joel), connection: true, repository: true);
        _sprintId = await DemoRunArrange.NewSprintAsync(database, project.Project.Id);
        await database.ExecuteAsync("UPDATE sprint SET rollover_status = 'decided' WHERE id = @_sprintId", new { _sprintId });
        await database.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id, role) VALUES (gen_random_uuid(), @id, @user, 'member')", new { project.Project.Id, user = SeedIds.Users.Priya });
        using var scope = Host.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IDemoRunStarter>().GetOrCreateItemAsync(project.Project.Id, _sprintId, "Sprint 23", CancellationToken.None);
        await database.ExecuteAsync(
            "UPDATE sprint SET demo_requested_at = @at, demo_requested_by = @by, demo_requested_via = 'button', demo_upload_expires_at = @expires WHERE id = @_sprintId",
            new { _sprintId, at = ViewerZoneTestData.LateEveningUtc, by = SeedIds.Users.Joel, expires = ViewerZoneTestData.LateEveningUtc.AddHours(24) });
        await ViewerZoneArrange.SetAsync(Api, TestUser.Joel, ViewerZoneTestData.BangkokId);
    }

    private async Task<SprintRunLabels> ReadAsync(TestUser user)
    {
        using var response = await Api.ActingAs(user).GetAsync($"api/sprints/{_sprintId}/demo");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK, text);
        var run = JsonSerializer.Deserialize<SprintDemoDto>(text, Wire)!.Run.ShouldNotBeNull();
        return new SprintRunLabels(run.RequestedLabel, run.UploadExpiresLabel);
    }

    private sealed record SprintRunLabels(string Requested, string? Expires);

    // Requested at 20:00 UTC two years ago, so the label is the short day with its year; the link expired 24 hours on. The expected
    // labels are written here from the instants, not taken from DisplayDate.
    private static string Expected(string prefix, DateTimeOffset at, int offsetHours)
    {
        var local = at.ToOffset(TimeSpan.FromHours(offsetHours));
        return $"{prefix} {local.ToString("d MMM yyyy", System.Globalization.CultureInfo.InvariantCulture)} at {local.ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture)}";
    }

    // The fallback: a user with no zone reads UTC.
    // Mutation: hardcode TimeZoneInfo.Utc at the DayAt calls in SprintDemoReader (then the Bangkok test fails; this one passes).
    [Test]
    public async Task DemoRead_WhenUserHasNoZone_ShouldUseUtc()
    {
        var labels = await ReadAsync(TestUser.Priya);

        var at = ViewerZoneTestData.LateEveningUtc;
        labels.Requested.ShouldBe(Expected("Requested", at, 0));
        labels.Expires.ShouldBe(Expected("Link expired", at.AddHours(24), 0));
        labels.Requested.ShouldEndWith("at 20:00");
    }

    // Mutation: hardcode TimeZoneInfo.Utc at the DayAt calls in SprintDemoReader.
    [Test]
    public async Task DemoRead_WhenUserIsInBangkok_ShouldUseBangkok()
    {
        var labels = await ReadAsync(TestUser.Joel);

        var at = ViewerZoneTestData.LateEveningUtc;
        labels.Requested.ShouldBe(Expected("Requested", at, 7));
        labels.Expires.ShouldBe(Expected("Link expired", at.AddHours(24), 7));
        labels.Requested.ShouldEndWith("at 03:00");
        labels.Requested.ShouldNotBe(Expected("Requested", at, 0));
    }
}
