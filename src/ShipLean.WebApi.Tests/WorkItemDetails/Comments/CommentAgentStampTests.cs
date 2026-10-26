using System.Net;
using System.Net.Http.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Contracts.Mcp;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Comments;

// A comment written while an agent acts for the user carries its name, through the real store behind the real endpoint.
// The ICurrentAgent is a double set per test; the signed-in user is the fixture's default (Joel).
[TestFixture]
public class CommentAgentStampTests : ApiFixture
{
    private readonly SettableCurrentAgent _agent = new();

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<ICurrentAgent>();
        services.AddSingleton<ICurrentAgent>(_agent);
    };

    [SetUp]
    public void NoAgentByDefault() => _agent.Clear();

    [Test]
    public async Task Add_WhenAgentActive_ShouldStampAgentName()
    {
        _agent.ActAs("Claude Code", "claude.ai", true);
        var verified = await AddAsync("From the agent");
        _agent.ActAs("Claude Code", null, false);
        var unverified = await AddAsync("From an unverified agent");
        _agent.Clear();
        var person = await AddAsync("From a person");

        verified.AgentName.ShouldBe("Claude Code (claude.ai)");
        unverified.AgentName.ShouldBe("Claude Code (unverified)");
        person.AgentName.ShouldBeNull();
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<string>("SELECT agent_name FROM comment WHERE id = @id", new { id = verified.Id })).ShouldBe("Claude Code (claude.ai)");
        (await connection.QuerySingleAsync<string?>("SELECT agent_name FROM comment WHERE id = @id", new { id = person.Id })).ShouldBeNull();
    }

    private async Task<CommentDto> AddAsync(string body)
    {
        using var response = await Api.PostJsonAsync($"api/work-items/{SeedIds.WorkItems.Ship90}/comments", new CommentCreate(body));
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
        return (await response.Content.ReadFromJsonAsync<CommentDto>())!;
    }
}
