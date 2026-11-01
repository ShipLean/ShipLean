using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Retros.Theming;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.Theming;

public sealed class PickRetroThemerTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    private PickRetroThemer Themer => Host.Services.GetRequiredService<PickRetroThemer>();

    [Test]
    public void Mode_ShouldBePick()
    {
        Themer.Mode.ShouldBe(RetroThemeMode.Pick);
    }

    [Test]
    public void Host_WhenFixtureDoesNotOptIn_ShouldResolveThemerToPick()
    {
        Host.Services.GetRequiredService<IRetroThemer>().ShouldBeOfType<PickRetroThemer>();
    }

    [Test]
    public async Task Assign_WhenExistingNameDifferentCase_ShouldReuse()
    {
        var name = $"Flaky waits {Guid.NewGuid():N}";
        await using var connection = await OpenDatabaseAsync();
        var existing = await new RetroSeed(connection, Project).ThemeAsync(name);

        var result = await AssignAsync($"  {name.ToUpperInvariant()} ", isPerson: false);

        result.ThemeId.ShouldBe(existing);
        result.SetBy.ShouldBe(RetroThemeSetBy.Agent);
    }

    [Test]
    public async Task Assign_WhenNewName_ShouldCreateWithAgentSetBy()
    {
        var name = $"New agent theme {Guid.NewGuid():N}";

        var result = await AssignAsync(name, isPerson: false);

        (await NameSetByAsync(result.ThemeId!.Value)).ShouldBe("agent");
        result.SetBy.ShouldBe(RetroThemeSetBy.Agent);
    }

    [Test]
    public async Task Assign_WhenPersonNewName_ShouldCreateWithPersonSetBy()
    {
        var name = $"New person theme {Guid.NewGuid():N}";

        var result = await AssignAsync(name, isPerson: true);

        (await NameSetByAsync(result.ThemeId!.Value)).ShouldBe("person");
        result.SetBy.ShouldBe(RetroThemeSetBy.Person);
    }

    [Test]
    public async Task Assign_WhenMergedTheme_ShouldFollowToTarget()
    {
        var suffix = Guid.NewGuid().ToString("N");
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, Project);
        var target = await seed.ThemeAsync($"Target {suffix}");
        var middle = await seed.MergedThemeAsync($"Middle {suffix}", target);
        await seed.MergedThemeAsync($"Source {suffix}", middle);

        var result = await AssignAsync($"source {suffix}", isPerson: false);

        result.ThemeId.ShouldBe(target);
    }

    [Test]
    public async Task Assign_WhenNoName_ShouldBeUnthemed()
    {
        foreach (var name in new string?[] { null, "", "   " })
        {
            var result = await AssignAsync(name, isPerson: false);

            result.ThemeId.ShouldBeNull();
            result.SetBy.ShouldBeNull();
        }
    }

    private Task<RetroThemeResult> AssignAsync(string? name, bool isPerson)
        => Themer.AssignAsync(new RetroThemeRequest(Project, "Some point", null, name, isPerson), CancellationToken.None);

    private async Task<string> NameSetByAsync(Guid themeId)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT name_set_by FROM retro_theme WHERE id = @themeId", new { themeId });
    }
}
