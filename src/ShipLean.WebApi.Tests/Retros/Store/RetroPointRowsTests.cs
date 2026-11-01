using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Store;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;

namespace ShipLean.WebApi.Tests.Retros.Store;

public sealed class RetroPointRowsTests : ApiFixture
{
    [Test]
    public async Task Insert_WhenMostPointsWentWell_ShouldCountAndTurnToneGood()
    {
        await using var connection = await OpenDatabaseAsync();
        var (retro, theme) = await ArrangeAsync(connection);

        await InsertAsync(connection, retro, theme, RetroCategory.WentWell, 0);
        await InsertAsync(connection, retro, theme, RetroCategory.WentBadly, 1);
        (await ThemeAsync(connection, theme)).ShouldBe((2, "bad"));
        await InsertAsync(connection, retro, theme, RetroCategory.WentWell, 2);

        (await ThemeAsync(connection, theme)).ShouldBe((3, "good"));
    }

    [Test]
    public async Task Insert_WhenMoreBadPointsFollow_ShouldTurnToneBackToBad()
    {
        await using var connection = await OpenDatabaseAsync();
        var (retro, theme) = await ArrangeAsync(connection);
        await InsertAsync(connection, retro, theme, RetroCategory.WentWell, 0);
        await InsertAsync(connection, retro, theme, RetroCategory.WentWell, 1);
        (await ThemeAsync(connection, theme)).ShouldBe((2, "good"));

        await InsertAsync(connection, retro, theme, RetroCategory.Improve, 2);
        await InsertAsync(connection, retro, theme, RetroCategory.NotInPlan, 3);
        await InsertAsync(connection, retro, theme, RetroCategory.WentBadly, 4);

        (await ThemeAsync(connection, theme)).ShouldBe((5, "bad"));
    }

    [Test]
    public async Task Insert_WhenUnthemed_ShouldStoreThePointAndTouchNoTheme()
    {
        await using var connection = await OpenDatabaseAsync();
        var (retro, theme) = await ArrangeAsync(connection);

        var id = await InsertAsync(connection, retro, null, RetroCategory.WentBadly, 7, "Because.");

        (await connection.QuerySingleAsync<(string RootCause, int Position, Guid? ThemeId)>(
            "SELECT root_cause AS RootCause, position AS Position, theme_id AS ThemeId FROM retro_point WHERE id = @id", new { id })).ShouldBe(("Because.", 7, null));
        (await ThemeAsync(connection, theme)).ShouldBe((0, "bad"));
    }

    private static async Task<(Guid Retro, Guid Theme)> ArrangeAsync(NpgsqlConnection connection)
    {
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: SeedIds.WorkItems.Ship90, AgentName: $"rows-{Guid.NewGuid():N}"));
        return (retro, await seed.ThemeAsync($"Theme {Guid.NewGuid():N}"));
    }

    private static async Task<Guid> InsertAsync(NpgsqlConnection connection, Guid retro, Guid? theme, RetroCategory category, int position, string? rootCause = null)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        var id = await RetroPointRows.InsertAsync(
            connection, transaction, retro, category, "A point.", rootCause,
            new RetroThemeResult(theme, theme is null ? null : RetroThemeSetBy.Agent, null, null), SeedIds.Users.Joel, "rows-agent", position, CancellationToken.None);
        await transaction.CommitAsync();
        return id;
    }

    private static async Task<(int Count, string Tone)> ThemeAsync(NpgsqlConnection connection, Guid theme)
        => await connection.QuerySingleAsync<(int, string)>("SELECT point_count AS Count, tone AS Tone FROM retro_theme WHERE id = @theme", new { theme });
}
