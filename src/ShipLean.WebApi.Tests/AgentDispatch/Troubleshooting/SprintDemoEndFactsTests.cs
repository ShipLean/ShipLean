using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Demos.Runs;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Demos;

namespace ShipLean.WebApi.Tests.AgentDispatch.Troubleshooting;

// The Demo band's facts (C6): SprintDemoReader reads the demo item's latest session's end reason and detail with C3 applied for
// the viewer. The facts are internal, so they are seen through the Failed StatusMessage, which SprintDemoStatusRule builds with
// AgentEndText.For.
// TODO: red until SHIP-1640 (A1a) makes SprintDemoStatusRule build the Failed message with AgentEndText.For and gives deliveryFailed
// its "<detail>" wording; the admin assertion then goes green. The member assertions hold today.
[TestFixture]
public class SprintDemoEndFactsTests : ApiFixture
{
    private const string ReceiverDetail = "The receiver answered 502 BadGateway at relay-host-7.internal:8443. (2 tries)";

    private const string OlderDetail = "An older session's error that the band must not show.";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private async Task<SprintDemoDto> DemoAsync(TestUser user, Guid sprintId)
    {
        using var response = await Api.ActingAs(user).GetAsync($"api/sprints/{sprintId}/demo");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<SprintDemoDto>(text, Wire)!;
    }

    // A closed sprint whose demo was requested an hour ago, with two failed sessions on its demo item since: an older agentError
    // and the latest, a deliveryFailed.
    private async Task<Guid> ArrangeAsync(NpgsqlConnection database)
    {
        var project = await AgentDispatchArrange.CreateAsync(database, Api.ActingAs(TestUser.Joel));
        await database.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @projectId, @userId, 'member')",
            new { id = Guid.NewGuid(), projectId = project.Project.Id, userId = SeedIds.Users.Priya });
        var sprintId = await DemoRunArrange.NewSprintAsync(database, project.Project.Id);
        using (var scope = Host.Services.CreateScope())
        {
            var itemId = await scope.ServiceProvider.GetRequiredService<IDemoRunStarter>().GetOrCreateItemAsync(project.Project.Id, sprintId, "Sprint 23", CancellationToken.None);
            var requested = TimeProvider.System.GetUtcNow().AddHours(-1);
            await database.ExecuteAsync(
                """
                UPDATE sprint SET rollover_status = 'decided', demo_requested_at = @requested, demo_requested_by = @by, demo_requested_via = 'button',
                                  demo_upload_expires_at = @expires
                WHERE id = @sprintId
                """,
                new { sprintId, requested, by = SeedIds.Users.Joel, expires = requested.AddHours(24) });
            var connectionId = project.ConnectionId!.Value;
            await new AgentSessionSeeds(itemId, connectionId).WithState("failed").WithEndReason(AgentEndReasons.AgentError).WithEndDetail(OlderDetail)
                .WithCreatedAt(requested.AddMinutes(1)).InsertAsync(database);
            await new AgentSessionSeeds(itemId, connectionId).WithState("failed").WithEndReason(AgentEndReasons.DeliveryFailed).WithEndDetail(ReceiverDetail)
                .WithCreatedAt(requested.AddMinutes(2)).InsertAsync(database);
        }

        return sprintId;
    }

    [Test]
    public async Task Get_WhenTheLatestSessionFailedToDeliver_ShouldNameItsDetailToAdminsOnly()
    {
        await using var database = await OpenDatabaseAsync();
        var sprintId = await ArrangeAsync(database);

        var admin = await DemoAsync(TestUser.Joel, sprintId);
        var member = await DemoAsync(TestUser.Priya, sprintId);

        admin.Status.ShouldBe(SprintDemoStatus.Failed);
        member.Status.ShouldBe(SprintDemoStatus.Failed);
        member.StatusMessage.ShouldNotBeNull();
        member.StatusMessage.ShouldNotContain(ReceiverDetail);
        member.StatusMessage.ShouldNotContain("relay-host-7");
        member.StatusMessage.ShouldNotContain(OlderDetail);
        admin.StatusMessage.ShouldNotBeNull();
        admin.StatusMessage.ShouldNotContain(OlderDetail);
        admin.StatusMessage.ShouldContain(ReceiverDetail);
    }
}
