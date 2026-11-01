using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Retros;

// list_retrospectives over /mcp. Every test arranges its own sprint or theme, so its filter picks out only its own points.
[TestFixture]
public class ListRetrospectivesTests : McpFixture
{
    private static readonly Guid Joel = SeedIds.Users.Joel;

    private string _bearer = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task IssueAToken() => _bearer = await TokenAsync(TestUser.Joel, ReadOnly);

    [Test]
    public async Task List_WhenFilteredBySprint_ShouldReturnOnlyThatSprintsPointsNewestFirst()
    {
        var (sprint, name) = await ArrangeSprintAsync();
        var other = (await ArrangeSprintAsync()).Id;
        var story = await ArrangeStoryAsync(sprint);
        var otherStory = await ArrangeStoryAsync(other);
        var older = DateTimeOffset.Parse("2026-01-02T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture);
        await SeedAsync(story, sprint, new RetroSeedPoint(RetroCategory.WentWell, "Older", CreatedAt: older), new RetroSeedPoint(RetroCategory.Improve, "Newer", CreatedAt: older.AddDays(5)));
        await SeedAsync(otherStory, other, new RetroSeedPoint(RetroCategory.WentWell, "Elsewhere"));

        var result = await ListAsync($$"""{"project":"SHIP","sprint":"{{name}}"}""");

        Texts(result).ShouldBe(["Newer", "Older"]);
        result.GetProperty("truncated").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task List_WhenFilteredByFeature_ShouldIncludeItsOwnRetroAndItsChildrens()
    {
        var feature = await ArrangeItemAsync(b => b.WithType("feature"));
        var child = await ArrangeItemAsync(b => b.WithType("story").WithParent(feature));
        var stranger = await ArrangeItemAsync(b => b.WithType("story"));
        await SeedAsync(feature, null, new RetroSeedPoint(RetroCategory.Improve, "Feature point"));
        await SeedAsync(child, null, new RetroSeedPoint(RetroCategory.WentWell, "Child point"));
        await SeedAsync(stranger, null, new RetroSeedPoint(RetroCategory.WentWell, "Stranger point"));

        var result = await ListAsync($$"""{"project":"SHIP","feature":"{{await KeyOfAsync(feature)}}"}""");

        Texts(result).Order().ShouldBe(["Child point", "Feature point"]);
        result.GetProperty("points")[0].GetProperty("item").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task List_WhenFilteredByCategory_ShouldReturnOnlyThatCategory()
    {
        var (sprint, name) = await ArrangeSprintAsync();
        var story = await ArrangeStoryAsync(sprint);
        await SeedAsync(story, sprint, new RetroSeedPoint(RetroCategory.WentWell, "Good"), new RetroSeedPoint(RetroCategory.WentBadly, "Bad", RootCause: "Because"), new RetroSeedPoint(RetroCategory.NotInPlan, "Surprise"));

        var bad = await ListAsync($$"""{"project":"SHIP","sprint":"{{name}}","category":"went_badly"}""");
        var surprise = await ListAsync($$"""{"project":"SHIP","sprint":"{{name}}","category":"not_in_plan"}""");

        Texts(bad).ShouldBe(["Bad"]);
        bad.GetProperty("points")[0].GetProperty("root_cause").GetString().ShouldBe("Because");
        Texts(surprise).ShouldBe(["Surprise"]);
    }

    [Test]
    public async Task List_WhenFilteredByTheme_ShouldReturnOnlyThatThemesPoints()
    {
        var themeName = $"List theme {Guid.NewGuid():N}";
        var story = await ArrangeStoryAsync(null);
        Guid theme;
        await using (var connection = await OpenDatabaseAsync())
        {
            theme = await new RetroSeed(connection, SeedIds.Projects.Ship).ThemeAsync(themeName);
        }

        await SeedAsync(story, null, new RetroSeedPoint(RetroCategory.Improve, "Themed", ThemeId: theme), new RetroSeedPoint(RetroCategory.Improve, "Unthemed"));

        var result = await ListAsync($$"""{"project":"SHIP","theme":"{{themeName.ToUpperInvariant()}}"}""");

        Texts(result).ShouldBe(["Themed"]);
        result.GetProperty("points")[0].GetProperty("theme").GetString().ShouldBe(themeName);
    }

    [Test]
    public async Task List_WhenSince_ShouldLeaveOutEarlierPoints()
    {
        var (sprint, name) = await ArrangeSprintAsync();
        var story = await ArrangeStoryAsync(sprint);
        await SeedAsync(
            story,
            sprint,
            new RetroSeedPoint(RetroCategory.WentWell, "Before", CreatedAt: DateTimeOffset.Parse("2026-03-09T23:59:00Z", System.Globalization.CultureInfo.InvariantCulture)),
            new RetroSeedPoint(RetroCategory.WentWell, "After", CreatedAt: DateTimeOffset.Parse("2026-03-10T00:01:00Z", System.Globalization.CultureInfo.InvariantCulture)));

        var result = await ListAsync($$"""{"project":"SHIP","sprint":"{{name}}","since":"2026-03-10"}""");
        var bad = await CallToolAsync(_bearer, "list_retrospectives", """{"project":"SHIP","since":"10 March"}""");

        Texts(result).ShouldBe(["After"]);
        bad.IsToolError().ShouldBeTrue();
    }

    [Test]
    public async Task List_WhenListed_ShouldReturnThemeListAndModeAndRetrosUrl()
    {
        var themeName = $"Listed theme {Guid.NewGuid():N}";
        var story = await ArrangeStoryAsync(null);
        Guid theme;
        await using (var connection = await OpenDatabaseAsync())
        {
            var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
            theme = await seed.ThemeAsync(themeName);
            await seed.ThemeAsync($"Empty theme {Guid.NewGuid():N}");
        }

        await SeedAsync(story, null, new RetroSeedPoint(RetroCategory.Improve, "Point", ThemeId: theme));

        var result = await ListAsync("""{"project":"SHIP"}""");

        result.GetProperty("theme_mode").GetString().ShouldBe("pick");
        var themes = result.GetProperty("themes").EnumerateArray().Select(t => t.GetProperty("name").GetString()).ToList();
        themes.ShouldContain(themeName);
        themes.ShouldNotContain(name => name!.StartsWith("Empty theme", StringComparison.Ordinal));
        result.GetProperty("open_url").GetString().ShouldEndWith("/p/SHIP/retros");
    }

    [Test]
    public async Task List_WhenPointArchived_ShouldLeaveItOut()
    {
        var (sprint, name) = await ArrangeSprintAsync();
        var story = await ArrangeStoryAsync(sprint);
        await SeedAsync(story, sprint, new RetroSeedPoint(RetroCategory.WentWell, "Kept"), new RetroSeedPoint(RetroCategory.WentWell, "Archived", Archived: true));

        var result = await ListAsync($$"""{"project":"SHIP","sprint":"{{name}}"}""");

        Texts(result).ShouldBe(["Kept"]);
    }

    [Test]
    public async Task List_WhenMoreThan200Points_ShouldCapAndSayTruncated()
    {
        var (sprint, name) = await ArrangeSprintAsync();
        var story = await ArrangeStoryAsync(sprint);
        var points = Enumerable.Range(0, 201).Select(i => new RetroSeedPoint(RetroCategory.WentWell, $"Point {i}", CreatedAt: DateTimeOffset.Parse("2026-04-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture).AddMinutes(i))).ToArray();
        await SeedAsync(story, sprint, points);

        var result = await ListAsync($$"""{"project":"SHIP","sprint":"{{name}}"}""");

        result.GetProperty("points").GetArrayLength().ShouldBe(200);
        result.GetProperty("truncated").GetBoolean().ShouldBeTrue();
        Texts(result)[0].ShouldBe("Point 200");
    }

    private async Task<JsonElement> ListAsync(string arguments)
    {
        var reply = await CallToolAsync(_bearer, "list_retrospectives", arguments);
        reply.IsToolError().ShouldBeFalse(reply.Body);
        return reply.Structured();
    }

    private static string[] Texts(JsonElement result)
        => [.. result.GetProperty("points").EnumerateArray().Select(p => p.GetProperty("text").GetString()!)];

    private async Task SeedAsync(Guid story, Guid? sprint, params RetroSeedPoint[] points)
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: sprint, AgentName: $"Seed {Guid.NewGuid():N}"));
        foreach (var point in points)
        {
            await seed.PointAsync(retro, point);
        }
    }

    private async Task<(Guid Id, string Name)> ArrangeSprintAsync()
    {
        var id = Guid.NewGuid();
        var name = $"Retro List {id:N}";
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO sprint (id, project_id, name, start_date, end_date) VALUES (@id, @project, @name, DATE '2003-01-01', DATE '2003-01-14')",
            new { id, project = SeedIds.Projects.Ship, name });
        return (id, name);
    }

    private Task<Guid> ArrangeStoryAsync(Guid? sprint)
        => ArrangeItemAsync(b => sprint is { } id ? b.WithType("story").WithSprint(id) : b.WithType("story"));

    private async Task<Guid> ArrangeItemAsync(Func<WorkItemBuilder, WorkItemBuilder> configure)
    {
        await using var connection = await OpenDatabaseAsync();
        return await configure(new WorkItemBuilder(SeedIds.Projects.Ship)).InsertAsync(connection);
    }

    private async Task<string> KeyOfAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
    }
}
