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
using ShipLean.WebApi.Tests.AgentDispatch;

namespace ShipLean.WebApi.Tests.Demos;

// The Demo band's Failed message says the end reason's sentence (K6 with C2/C3): the full sentence for a project admin, and for a
// member the sentence without the receiver's detail.
[TestFixture]
public class SprintDemoEndReasonTests : ApiFixture
{
    private const string Detail = "No answer within 5 seconds. (2 tries)";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private async Task<SprintDemoDto> DemoAsync(TestUser user, Guid sprintId)
    {
        using var response = await Api.ActingAs(user).GetAsync($"api/sprints/{sprintId}/demo");
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<SprintDemoDto>(text, Wire)!;
    }

    private async Task<Guid> ArrangeAsync(NpgsqlConnection database, string state, string reason, string? detail)
    {
        var project = await AgentDispatchArrange.CreateAsync(database, Api.ActingAs(TestUser.Joel));
        await database.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @projectId, @userId, 'member')",
            new { id = Guid.NewGuid(), projectId = project.Project.Id, userId = SeedIds.Users.Priya });
        var sprintId = await DemoRunArrange.NewSprintAsync(database, project.Project.Id);
        using var scope = Host.Services.CreateScope();
        var itemId = await scope.ServiceProvider.GetRequiredService<IDemoRunStarter>().GetOrCreateItemAsync(project.Project.Id, sprintId, "Sprint 23", CancellationToken.None);
        var requested = TimeProvider.System.GetUtcNow().AddHours(-1);
        await database.ExecuteAsync(
            """
            UPDATE sprint SET rollover_status = 'decided', demo_requested_at = @requested, demo_requested_by = @by, demo_requested_via = 'button',
                              demo_upload_expires_at = @expires
            WHERE id = @sprintId
            """,
            new { sprintId, requested, by = SeedIds.Users.Joel, expires = requested.AddHours(24) });
        await new AgentSessionSeeds(itemId, project.ConnectionId!.Value).WithState(state).WithEndReason(reason).WithEndDetail(detail)
            .WithCreatedAt(requested.AddMinutes(2)).InsertAsync(database);
        return sprintId;
    }

    [Test]
    public async Task Get_WhenTheLatestSessionFailedToDeliver_ShouldGiveAdminsTheFullSentenceAndMembersNoDetail()
    {
        await using var database = await OpenDatabaseAsync();
        var sprintId = await ArrangeAsync(database, "failed", AgentEndReasons.DeliveryFailed, Detail);

        var admin = await DemoAsync(TestUser.Joel, sprintId);
        var member = await DemoAsync(TestUser.Priya, sprintId);

        admin.Status.ShouldBe(SprintDemoStatus.Failed);
        admin.StatusMessage.ShouldBe($"The agent connection didn't take the job: {Detail}");
        member.Status.ShouldBe(SprintDemoStatus.Failed);
        member.StatusMessage.ShouldBe(AgentEndText.For(AgentEndReasons.DeliveryFailed, null));
        member.StatusMessage.ShouldBe("The agent connection didn't take the job.");
    }

    [Test]
    public async Task Get_WhenTheLatestSessionWentStale_ShouldSayTheAgentWentQuiet()
    {
        await using var database = await OpenDatabaseAsync();
        var sprintId = await ArrangeAsync(database, "stale", AgentEndReasons.Stale, "No activity for 30 minutes since 2026-11-16T01:00:00Z.");

        var member = await DemoAsync(TestUser.Priya, sprintId);

        member.StatusMessage.ShouldBe("The agent went quiet for 30 minutes.");
    }
}
