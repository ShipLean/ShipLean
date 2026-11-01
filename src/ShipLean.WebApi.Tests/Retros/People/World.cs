using Npgsql;
using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.ProjectSettings.Api;
using ShipLean.WebApi.Tests.Sprints.Api;

namespace ShipLean.WebApi.Tests.Retros.People;

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
