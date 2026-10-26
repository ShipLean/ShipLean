using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.Mcp;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItems.Api;

// Revisions written while an agent is acting carry its name, through the real store behind the real endpoints. The
// ICurrentAgent is a double set per test; the signed-in user is the fixture's default (Joel).
[TestFixture]
public class WorkItemAgentStampTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    private readonly SettableCurrentAgent _agent = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<ICurrentAgent>();
        services.AddSingleton<ICurrentAgent>(_agent);
    };

    [SetUp]
    public void NoAgentByDefault() => _agent.Clear();

    [Test]
    public async Task Write_WhenAgentActive_ShouldStampRevisionAgentName()
    {
        _agent.ActAs("Claude Code", "claude.ai", true);
        var created = await CreateAsync("Agent made this");

        _agent.ActAs("Claude Code", null, false);
        var patched = await PatchTitleAsync(created.Id, 1, "Agent renamed this");

        (await AgentNamesAsync(created.Id)).ShouldBe([(1, "Claude Code (claude.ai)"), (2, "Claude Code (unverified)")]);
        patched.Revision.ShouldBe(2);
    }

    [Test]
    public async Task Write_WhenNoAgent_ShouldLeaveAgentNameNull()
    {
        var created = await CreateAsync("A person made this");
        await PatchTitleAsync(created.Id, 1, "A person renamed this");

        (await AgentNamesAsync(created.Id)).ShouldBe([(1, null), (2, null)]);
    }

    [Test]
    public async Task Conflict_WhenAgentWroteTheTheirChanges_ShouldReadAgentNameBack()
    {
        var created = await CreateAsync("Raced");
        _agent.ActAs("Claude Code", "claude.ai", true);
        await PatchTitleAsync(created.Id, 1, "Agent got there first");
        _agent.Clear();

        using var stale = await Api.PatchJsonAsync($"api/work-items/{created.Id}", PatchBody(1, "Too late"));

        stale.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var conflict = JsonSerializer.Deserialize<ConflictDto>(await stale.Content.ReadAsStringAsync(), Wire)!;
        conflict.TheirChanges.Select(r => r.AgentName).ShouldBe(["Claude Code (claude.ai)"]);
    }

    private static JsonNode PatchBody(int expectedRevision, string title)
        => new JsonObject { ["expectedRevision"] = expectedRevision, ["changes"] = new JsonObject { ["title"] = new JsonObject { ["value"] = title } } };

    private async Task<WriteResult> CreateAsync(string title)
    {
        using var response = await Api.PostJsonAsync("api/work-items", new JsonObject
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = "story",
            ["title"] = title,
            ["parentId"] = null,
            ["sprintId"] = null,
            ["changes"] = null,
        });
        return await ReadAsync<WriteResult>(response);
    }

    private async Task<WriteResult> PatchTitleAsync(Guid id, int expectedRevision, string title)
    {
        using var response = await Api.PatchJsonAsync($"api/work-items/{id}", PatchBody(expectedRevision, title));
        return await ReadAsync<WriteResult>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(HttpStatusCode.OK, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }

    private async Task<List<(int Rev, string? AgentName)>> AgentNamesAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        var rows = await connection.QueryAsync<(int Rev, string? AgentName)>(
            "SELECT rev, agent_name AS agentName FROM revision WHERE work_item_id = @id ORDER BY rev", new { id });
        return [.. rows];
    }
}
