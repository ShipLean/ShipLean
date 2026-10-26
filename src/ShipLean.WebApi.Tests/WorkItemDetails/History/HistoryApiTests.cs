using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.WorkItemDetails.History;

// GET api/work-items/{id}/history through the real app: revisions newest first with who changed what, an agent's name when it
// made the change, Jira revisions marked. Joel is the project admin; Kai is in no project.
[TestFixture]
public class HistoryApiTests : ApiFixture
{
    private const string Title = """[{"field":"title","from":"Old","to":"New","fromLabel":null,"toLabel":null}]""";
    private const string Points = """[{"field":"points","from":3,"to":5,"fromLabel":null,"toLabel":null}]""";

    [Test]
    public async Task History_WhenOpened_ShouldListFieldChangesNewestFirst()
    {
        var id = await ArrangeItemAsync();
        await using (var connection = await OpenDatabaseAsync())
        {
            await new RevisionBuilder(id).WithRev(2).WithChangedBy(SeedIds.Users.Mei).WithChangedAt(new DateTimeOffset(2026, 3, 2, 9, 0, 0, TimeSpan.Zero)).WithChanges(Title).InsertAsync(connection);
            await new RevisionBuilder(id).WithRev(3).WithChangedBy(SeedIds.Users.Priya).WithChangedAt(new DateTimeOffset(2026, 3, 3, 9, 0, 0, TimeSpan.Zero)).WithChanges(Points).InsertAsync(connection);
        }

        var history = await ReadHistoryAsync(id);

        history.Revisions.Select(r => r.Rev).ShouldBe([3, 2, 1]);
        history.Revisions[0].ChangedBy.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Priya);
        history.Revisions[0].Changes.ShouldHaveSingleItem().Field.ShouldBe("points");
        history.Revisions[1].ChangedBy.ShouldNotBeNull().Id.ShouldBe(SeedIds.Users.Mei);
        history.Revisions[0].ChangedAtLabel.ShouldNotBeNullOrWhiteSpace();
        history.Revisions.ShouldAllBe(r => r.Origin == RevisionOrigin.User && r.AgentName == null);
    }

    [Test]
    public async Task History_WhenAnAgentMadeTheChange_ShouldNameTheAgent()
    {
        var id = await ArrangeItemAsync();
        await using (var connection = await OpenDatabaseAsync())
        {
            await new RevisionBuilder(id).WithRev(2).WithChangedBy(SeedIds.Users.Mei).WithChanges(Title).InsertAsync(connection);
            await connection.ExecuteAsync("UPDATE revision SET agent_name = 'Claude Code (claude.ai)' WHERE work_item_id = @id AND rev = 2", new { id });
        }

        var history = await ReadHistoryAsync(id);

        history.Revisions.Single(r => r.Rev == 2).AgentName.ShouldBe("Claude Code (claude.ai)");
        history.Revisions.Single(r => r.Rev == 1).AgentName.ShouldBeNull();
    }

    [Test]
    public async Task History_WhenNotMember_ShouldBeNotFound()
    {
        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"api/work-items/{SeedIds.WorkItems.Ship90}/history");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    private async Task<HistoryDto> ReadHistoryAsync(Guid id)
    {
        using var response = await Api.GetAsync($"api/work-items/{id}/history");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return JsonSerializer.Deserialize<HistoryDto>(await response.Content.ReadAsStringAsync(), PersistedJson.Options)!;
    }

    private async Task<Guid> ArrangeItemAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await new WorkItemBuilder(SeedIds.Projects.Ship).InsertAsync(connection);
    }
}
