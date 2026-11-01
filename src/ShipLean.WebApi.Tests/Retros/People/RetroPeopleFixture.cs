using System.Net;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Events;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Retros.People;

// The host and helpers the people's retro write tests share. Every test arranges a project of its own with Joel as a member,
// so the themes, retros and counts it asserts on are its alone.
public abstract class RetroPeopleFixture : ApiFixture
{
    protected ProjectEventWatch Watch(Guid projectId) => new(Host.Services.GetRequiredService<IProjectEvents>(), projectId);

    protected static async Task<JsonElement> ReadAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    protected async Task<JsonElement> PostPointAsync(Guid projectId, object body)
    {
        using var response = await Api.PostJsonAsync($"/api/projects/{projectId}/retros/points", body);
        return await ReadAsync(response, HttpStatusCode.Created);
    }
}

// A project of the test's own with one sprint. It owns the connection until the test ends.
internal sealed record World(NpgsqlConnection Connection, ArrangedProject Project, Guid Sprint) : IAsyncDisposable
{
    private static readonly Guid Joel = SeedIds.Users.Joel;

    public static async Task<World> CreateAsync(NpgsqlConnection connection)
    {
        var project = await ArrangedProject.CreateAsync(connection, (Joel, ProjectRole.Member));
        var sprint = await RetroArrangement.SprintAsync(connection, project.Id, "S1", new DateOnly(2026, 1, 5), new DateOnly(2026, 1, 18));
        return new World(connection, project, sprint);
    }

    public RetroSeed Seed { get; } = new(Connection, Project.Id);

    public ValueTask DisposeAsync() => Connection.DisposeAsync();

    public Task<Guid> ItemAsync(string type, bool inSprint = true)
    {
        var builder = new WorkItemBuilder(Project.Id).WithType(type);
        return (inSprint ? builder.WithSprint(Sprint) : builder).InsertAsync(Connection);
    }

    // A retro an agent logged on the sprint, with the points the caller adds.
    public Task<Guid> AgentRetroAsync() => Seed.RetroAsync(new RetroSeedRetro(Joel, ShipLean.Contracts.Retros.RetroRole.SubAgent, SprintId: Sprint, AgentName: $"agent-{Guid.NewGuid():N}"));
}
