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
    public async Task List_WhenFeatureKeyIsAStory_ShouldBeValidationError()
    {
        var story = await ArrangeItemAsync(b => b.WithType("story"));

        var reply = await CallToolAsync(_bearer, "list_retrospectives", $$"""{"project":"SHIP","feature":"{{await KeyOfAsync(story)}}"}""");

        reply.IsToolError().ShouldBeTrue(reply.Body);
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("validation");
        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("feature");
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

    [Test]
    public async Task List_WhenActioned_ShouldReturnActionKeyAndState()
    {
        var story = await ArrangeItemAsync(b => b.WithType("story"));
        var action = await ArrangeItemAsync(b => b.WithType("action"));
        var (pointId, theme) = (Guid.Empty, $"Actioned {Guid.NewGuid():N}");
        var themeId = await ArrangeThemeAsync(theme);
        pointId = (await SeedPointsAsync(story, null, new RetroSeedPoint(RetroCategory.Improve, "Fix it", ThemeId: themeId)))[0];
        await using (var connection = await OpenDatabaseAsync())
        {
            await new RetroSeed(connection, SeedIds.Projects.Ship).ActionAsync(new RetroSeedAction(action, PointId: pointId));
        }

        var result = await ListAsync($$"""{"project":"SHIP","theme":"{{theme}}"}""");

        var line = result.GetProperty("points")[0];
        line.GetProperty("decision").GetString().ShouldBe("actioned");
        line.GetProperty("action").GetProperty("key").GetString().ShouldBe(await KeyOfAsync(action));
        line.GetProperty("action").GetProperty("state").GetString().ShouldBe(await StateNameOfAsync(action));
        line.GetProperty("not_needed_reason").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task List_WhenNotNeeded_ShouldReturnTheReason()
    {
        var (sprint, name) = await ArrangeSprintAsync();
        var story = await ArrangeStoryAsync(sprint);
        await SeedAsync(story, sprint, new RetroSeedPoint(RetroCategory.Improve, "Skip it", NotNeededReason: "Fixed upstream"));

        var line = (await ListAsync($$"""{"project":"SHIP","sprint":"{{name}}"}""")).GetProperty("points")[0];

        line.GetProperty("decision").GetString().ShouldBe("notNeeded");
        line.GetProperty("not_needed_reason").GetString().ShouldBe("Fixed upstream");
        line.GetProperty("action").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task List_WhenWentWell_ShouldHaveNullDecision()
    {
        var (sprint, name) = await ArrangeSprintAsync();
        var story = await ArrangeStoryAsync(sprint);
        await SeedAsync(story, sprint, new RetroSeedPoint(RetroCategory.WentWell, "Nice"));

        var line = (await ListAsync($$"""{"project":"SHIP","sprint":"{{name}}"}""")).GetProperty("points")[0];

        line.GetProperty("decision").ValueKind.ShouldBe(JsonValueKind.Null);
        line.GetProperty("action").ValueKind.ShouldBe(JsonValueKind.Null);
        line.GetProperty("not_needed_reason").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task List_WhenUndecided_ShouldLeaveOutDecidedAndWentWell()
    {
        var (sprint, name) = await ArrangeSprintAsync();
        var story = await ArrangeStoryAsync(sprint);
        var action = await ArrangeItemAsync(b => b.WithType("action"));
        var ids = await SeedPointsAsync(
            story,
            sprint,
            new RetroSeedPoint(RetroCategory.Improve, "Open"),
            new RetroSeedPoint(RetroCategory.WentWell, "Nice"),
            new RetroSeedPoint(RetroCategory.WentBadly, "Skipped", NotNeededReason: "No"),
            new RetroSeedPoint(RetroCategory.NotInPlan, "Done"));
        await using (var connection = await OpenDatabaseAsync())
        {
            await new RetroSeed(connection, SeedIds.Projects.Ship).ActionAsync(new RetroSeedAction(action, PointId: ids[3]));
        }

        var result = await ListAsync($$"""{"project":"SHIP","sprint":"{{name}}","undecided":true}""");

        Texts(result).ShouldBe(["Open"]);
    }

    [Test]
    public async Task List_WhenManyDecided_ShouldStillReachUndecided()
    {
        var (sprint, name) = await ArrangeSprintAsync();
        var story = await ArrangeStoryAsync(sprint);
        var decided = Enumerable.Range(0, 205).Select(i => new RetroSeedPoint(RetroCategory.Improve, $"Decided {i}", NotNeededReason: "No", CreatedAt: DateTimeOffset.Parse("2026-05-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture).AddMinutes(i)));
        await SeedAsync(story, sprint, [.. decided, new RetroSeedPoint(RetroCategory.Improve, "Oldest open", CreatedAt: DateTimeOffset.Parse("2026-04-01T00:00:00Z", System.Globalization.CultureInfo.InvariantCulture))]);

        var result = await ListAsync($$"""{"project":"SHIP","sprint":"{{name}}","undecided":true}""");

        Texts(result).ShouldBe(["Oldest open"]);
        result.GetProperty("truncated").GetBoolean().ShouldBeFalse();
    }

    [Test]
    public async Task List_WhenNoSprint_ShouldOnlyReturnNoSprintPoints()
    {
        var (sprint, _) = await ArrangeSprintAsync();
        var theme = $"No sprint {Guid.NewGuid():N}";
        var themeId = await ArrangeThemeAsync(theme);
        var inSprint = await ArrangeStoryAsync(sprint);
        var without = await ArrangeStoryAsync(null);
        await SeedAsync(inSprint, sprint, new RetroSeedPoint(RetroCategory.Improve, "With sprint", ThemeId: themeId));
        await SeedAsync(without, null, new RetroSeedPoint(RetroCategory.Improve, "Without sprint", ThemeId: themeId));

        var result = await ListAsync($$"""{"project":"SHIP","theme":"{{theme}}","no_sprint":true}""");

        Texts(result).ShouldBe(["Without sprint"]);
    }

    [Test]
    public async Task List_WhenNoSprintAndSprint_ShouldBeValidation()
    {
        var (_, name) = await ArrangeSprintAsync();

        var reply = await CallToolAsync(_bearer, "list_retrospectives", $$"""{"project":"SHIP","sprint":"{{name}}","no_sprint":true}""");

        reply.IsToolError().ShouldBeTrue(reply.Body);
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("validation");
        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("no_sprint");
    }

    [Test]
    public async Task List_WhenFilteredByFeature_ShouldLeaveOutItsActionItemsRetros()
    {
        var feature = await ArrangeItemAsync(b => b.WithType("feature"));
        var story = await ArrangeItemAsync(b => b.WithType("story").WithParent(feature));
        var actionChild = await ArrangeItemAsync(b => b.WithType("action").WithParent(feature));
        await SeedAsync(story, null, new RetroSeedPoint(RetroCategory.Improve, "Story point"));
        await SeedAsync(actionChild, null, new RetroSeedPoint(RetroCategory.Improve, "Action point"));

        var result = await ListAsync($$"""{"project":"SHIP","feature":"{{await KeyOfAsync(feature)}}"}""");

        Texts(result).ShouldBe(["Story point"]);
    }

    private async Task<Guid> ArrangeThemeAsync(string name)
    {
        await using var connection = await OpenDatabaseAsync();
        return await new RetroSeed(connection, SeedIds.Projects.Ship).ThemeAsync(name);
    }

    private async Task<string> StateNameOfAsync(Guid id)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>("SELECT s.name FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.id = @id", new { id });
    }

    private async Task<List<Guid>> SeedPointsAsync(Guid story, Guid? sprint, params RetroSeedPoint[] points)
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await seed.RetroAsync(new RetroSeedRetro(Joel, WorkItemId: story, SprintId: sprint, AgentName: $"Seed {Guid.NewGuid():N}"));
        var ids = new List<Guid>();
        foreach (var point in points)
        {
            ids.Add(await seed.PointAsync(retro, point));
        }

        return ids;
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
