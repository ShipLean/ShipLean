using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.Application.Features.Retros.Theming;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Search;
using ShipLean.Application.Features.Search.Embedding;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Retros;
using ShipLean.TestSupport.Search;

namespace ShipLean.WebApi.Tests.Retros.Theming;

// Auto mode over the FakeEmbedder: two texts with the same concepts embed identically, texts with no shared concept sit near 0.
public sealed class RetroThemerTests : ApiFixture
{
    private static readonly Guid Project = SeedIds.Projects.Ship;

    protected override Action<IServiceCollection>? ConfigureTestServices => RetroThemerServices.Register;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [RetroThemerServices.AutoThemesKey] = "on" };

    private IRetroThemer Themer => Host.Services.GetRequiredService<IRetroThemer>();

    private FakeEmbedder Embedder => (FakeEmbedder)Host.Services.GetRequiredService<IEmbedder>();

    [SetUp]
    public async Task StartFromNoThemes()
    {
        Embedder.ModelId = new EmbeddingModelId("fake-embedder", "1");
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE retro_theme SET archived_at = now() WHERE project_id = @Project AND archived_at IS NULL", new { Project });
    }

    [Test]
    public async Task Assign_WhenSimilarToExisting_ShouldJoinTheme()
    {
        var first = await AssignAsync("Sign-in fails behind the proxy");

        var second = await AssignAsync("Login broken behind the proxy");

        second.ThemeId.ShouldBe(first.ThemeId);
        second.SetBy.ShouldBe(RetroThemeSetBy.Auto);
        second.Embedding.ShouldNotBeNull();
    }

    [Test]
    public async Task Assign_WhenDissimilar_ShouldCreateNamedTheme()
    {
        var first = await AssignAsync("Sign-in fails behind the proxy");

        var second = await AssignAsync("Catering budget exceeded for the quarterly offsite. Everyone was hungry.");

        second.ThemeId.ShouldNotBe(first.ThemeId);
        var themeId = second.ThemeId!.Value;
        (await NameAsync(themeId)).ShouldBe("Catering budget exceeded for the quarterly");
        (await NameSetByAsync(themeId)).ShouldBe("auto");
        (await CentroidModelAsync(themeId)).ShouldBe("fake-embedder@1");
        (await CentroidAsync(themeId)).ShouldNotBeNull();
    }

    [Test]
    public async Task Assign_WhenJoining_ShouldUpdateCentroid()
    {
        var first = await AssignAsync("Sign-in fails behind the proxy");
        var before = await CentroidAsync(first.ThemeId!.Value);

        await AssignAsync("Login broken behind the proxy page");

        var after = await CentroidAsync(first.ThemeId.Value);
        after.ShouldNotBe(before);
    }

    [Test]
    public async Task Assign_WhenThemeSuppliedInAutoMode_ShouldIgnoreWithWarning()
    {
        var result = await Themer.AssignAsync(new RetroThemeRequest(Project, "Sign-in fails behind the proxy", null, "My own theme", false), CancellationToken.None);

        result.SetBy.ShouldBe(RetroThemeSetBy.Auto);
        (await NameAsync(result.ThemeId!.Value)).ShouldBe("Sign-in fails behind the proxy");
        result.Warning.ShouldBeNull("RetroLog adds the supplied-theme warning itself");
    }

    [Test]
    public async Task Mode_WhenEmbedderNotSemantic_ShouldBePickAndDelegate()
    {
        var pick = Host.Services.GetRequiredService<PickRetroThemer>();
        var themer = new RetroThemer(Host.Services.GetRequiredService<NpgsqlDataSource>(), new NoEmbedder("off"), pick, true);
        var name = $"Picked {Guid.NewGuid():N}";

        var result = await themer.AssignAsync(new RetroThemeRequest(Project, "Sign-in fails", null, name, false), CancellationToken.None);

        themer.Mode.ShouldBe(RetroThemeMode.Pick);
        result.SetBy.ShouldBe(RetroThemeSetBy.Agent);
        result.Embedding.ShouldBeNull();
        (await NameAsync(result.ThemeId!.Value)).ShouldBe(name);
    }

    [Test]
    public void Mode_WhenSwitchOnAndEmbedderSemantic_ShouldBeAuto()
    {
        Themer.Mode.ShouldBe(RetroThemeMode.Auto);
    }

    [Test]
    public async Task Mode_WhenSwitchOffAndEmbedderSemantic_ShouldBePickAndHonourSuppliedTheme()
    {
        var themer = new RetroThemer(Host.Services.GetRequiredService<NpgsqlDataSource>(), Embedder, Host.Services.GetRequiredService<PickRetroThemer>(), false);
        var name = $"Picked {Guid.NewGuid():N}";

        var result = await themer.AssignAsync(new RetroThemeRequest(Project, "Sign-in fails", null, name, false), CancellationToken.None);

        themer.Mode.ShouldBe(RetroThemeMode.Pick);
        result.SetBy.ShouldBe(RetroThemeSetBy.Agent);
        (await NameAsync(result.ThemeId!.Value)).ShouldBe(name);
    }

    [Test]
    public async Task Assign_WhenMergedTheme_ShouldNotJoinIt()
    {
        var first = await AssignAsync("Sign-in fails behind the proxy");
        await using (var connection = await OpenDatabaseAsync())
        {
            var target = await new RetroSeed(connection, Project).ThemeAsync($"Target {Guid.NewGuid():N}");
            await connection.ExecuteAsync("UPDATE retro_theme SET merged_into_id = @target WHERE id = @id", new { target, id = first.ThemeId });
        }

        var second = await AssignAsync("Login broken behind the proxy");

        second.ThemeId.ShouldNotBe(first.ThemeId);
    }

    [Test]
    public async Task Assign_WhenCentroidFromOtherModel_ShouldNotCompareAndShouldReseed()
    {
        var first = await AssignAsync("Sign-in fails behind the proxy");
        await using (var connection = await OpenDatabaseAsync())
        {
            var retro = await new RetroSeed(connection, Project).RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, RetroRole.Person, SprintId: SeedIds.Sprints.Sprint19));
            await new RetroSeed(connection, Project).PointAsync(retro, new RetroSeedPoint(RetroCategory.WentBadly, "Sign-in fails behind the proxy", ThemeId: first.ThemeId, ThemeSetBy: RetroThemeSetBy.Auto));
            await connection.ExecuteAsync("UPDATE retro_theme SET point_count = 1 WHERE id = @id", new { id = first.ThemeId });
        }

        Embedder.ModelId = new EmbeddingModelId("fake-embedder", "2");
        var second = await AssignAsync("Login broken behind the proxy");

        second.ThemeId.ShouldNotBe(first.ThemeId);
        (await CentroidModelAsync(first.ThemeId!.Value)).ShouldBe("fake-embedder@2");
    }

    [Test]
    public async Task Assign_WhenWriterTransactionHoldsTheThemeRow_ShouldNotWaitOnIt()
    {
        var first = await AssignAsync("Sign-in fails behind the proxy");
        await using var connection = await OpenDatabaseAsync();
        await using var transaction = await connection.BeginTransactionAsync();
        await connection.ExecuteAsync("UPDATE retro_theme SET point_count = point_count + 1 WHERE id = @id", new { id = first.ThemeId }, transaction);

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var second = await AssignAsync("Login broken behind the proxy", timeout.Token);
        await transaction.CommitAsync();

        second.ThemeId.ShouldBe(first.ThemeId);
    }

    [Test]
    public async Task Assign_WhenNameTaken_ShouldNumberTheNewTheme()
    {
        await AssignAsync("Catering budget exceeded");

        var second = await AssignAsync("Catering budget exceeded. Payment pending.");

        (await NameAsync(second.ThemeId!.Value)).ShouldBe("Catering budget exceeded (2)");
    }

    private Task<RetroThemeResult> AssignAsync(string text, CancellationToken cancellationToken = default)
        => Themer.AssignAsync(new RetroThemeRequest(Project, text, null, null, false), cancellationToken);

    private async Task<string> ColumnAsync(Guid id, string column)
    {
        await using var connection = await OpenDatabaseAsync();
        return (await connection.ExecuteScalarAsync<string?>($"SELECT {column} FROM retro_theme WHERE id = @id", new { id }))!;
    }

    private Task<string> NameAsync(Guid id) => ColumnAsync(id, "name");

    private Task<string> NameSetByAsync(Guid id) => ColumnAsync(id, "name_set_by");

    private async Task<string> CentroidModelAsync(Guid id) => await ColumnAsync(id, "centroid_model");

    private async Task<string> CentroidAsync(Guid id) => await ColumnAsync(id, "centroid::text");
}
