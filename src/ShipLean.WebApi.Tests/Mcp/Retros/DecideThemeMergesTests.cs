using System.Reflection;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Retros;

// decide_theme_merges: lists the proposals an agent left, and answers them through the same IRetroMergeProposals the web uses. Each test proposes its own
// uniquely named themes, so what one lists or answers never reaches another.
[TestFixture]
public class DecideThemeMergesTests : McpFixture
{
    private static readonly RetroActor Agent = new(SeedIds.Users.Joel, "claude-x");

    protected override bool WithProbeTools => false;

    [Test]
    public void Tool_ShouldBeRegisteredWithPlanScope()
    {
        var tool = typeof(ApiMarker).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            .Single(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name == "decide_theme_merges");

        var scope = tool.GetCustomAttribute<McpScopeAttribute>()!;

        scope.Scope.ShouldBe(McpScopes.Plan);
        scope.PreviewScope.ShouldBe(McpScopes.Read);
    }

    [Test]
    public async Task List_ShouldShowThePendingProposalsWithNamesReasonProposerAndPointCount()
    {
        var world = await WorldAsync();
        var from = await world.ThemeAsync("From");
        await world.ThemeAsync("Into");
        await world.PointsOnAsync(from, 2);
        await world.ProposeAsync("From", "Into");

        var result = await world.CallAsync(ReadOnly, "{}");

        var line = result.GetProperty("pending").EnumerateArray().Single(l => l.GetProperty("from").GetString() == world.Name("From"));
        line.GetProperty("into").GetString().ShouldBe(world.Name("Into"));
        line.GetProperty("reason").GetString().ShouldBe("Same.");
        line.GetProperty("proposed_by").GetString().ShouldBe("claude-x");
        line.GetProperty("point_count").GetInt32().ShouldBe(2);
        result.GetProperty("applied").GetBoolean().ShouldBeFalse();
        result.TryGetProperty("plan", out var plan).ShouldBeTrue();
        plan.ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public async Task Preview_ShouldReturnAPlanAndChangeNothing()
    {
        var world = await WorldAsync();
        var (id, _) = await world.ProposedAsync("From", "Into");

        var preview = await world.CallAsync(ReadOnly, $$"""{"merge":["{{id}}"]}""");

        preview.GetProperty("plan").GetString().ShouldNotBeNullOrEmpty();
        preview.GetProperty("merge").GetArrayLength().ShouldBe(1);
        (await world.StatusAsync(id)).ShouldBe("pending");
    }

    [Test]
    public async Task Apply_WhenAccepting_ShouldMovePointsIncludingAPersonsAndRemoveTheTheme()
    {
        var world = await WorldAsync();
        var (id, themes) = await world.ProposedAsync("From", "Into");
        var agentPoint = await world.PointOnAsync(themes.From, RetroThemeSetBy.Agent);
        var personPoint = await world.PointOnAsync(themes.From, RetroThemeSetBy.Person);
        var plan = (await world.CallAsync(ReadOnly, $$"""{"merge":["{{id}}"]}""")).GetProperty("plan").GetString()!;

        var applied = await world.CallAsync(ReadWritePlan, $$"""{"plan":"{{plan}}"}""");

        applied.GetProperty("applied").GetBoolean().ShouldBeTrue();
        applied.GetProperty("merge").GetArrayLength().ShouldBe(1);
        (await world.ThemeOfAsync(agentPoint)).ShouldBe(themes.Into);
        (await world.ThemeOfAsync(personPoint)).ShouldBe(themes.Into);
        (await world.MergedIntoAsync(themes.From)).ShouldBe(themes.Into);
        (await world.StatusAsync(id)).ShouldBe("merged");
        applied.GetProperty("pending").EnumerateArray().Any(l => l.GetProperty("id").GetGuid() == id).ShouldBeFalse();
    }

    [Test]
    public async Task Apply_WhenRejecting_ShouldKeepThemesAndPointsApart()
    {
        var world = await WorldAsync();
        var (id, themes) = await world.ProposedAsync("From", "Into");
        var point = await world.PointOnAsync(themes.From, RetroThemeSetBy.Agent);
        var plan = (await world.CallAsync(ReadOnly, $$"""{"keep_apart":["{{id}}"]}""")).GetProperty("plan").GetString()!;

        var applied = await world.CallAsync(ReadWritePlan, $$"""{"plan":"{{plan}}"}""");

        applied.GetProperty("keep_apart").GetArrayLength().ShouldBe(1);
        (await world.ThemeOfAsync(point)).ShouldBe(themes.From);
        (await world.MergedIntoAsync(themes.From)).ShouldBeNull();
        (await world.StatusAsync(id)).ShouldBe("keptApart");
    }

    [Test]
    public async Task Preview_WhenProposalAlreadyAnswered_ShouldBeConflict()
    {
        var world = await WorldAsync();
        var (id, _) = await world.ProposedAsync("From", "Into");
        var plan = (await world.CallAsync(ReadOnly, $$"""{"merge":["{{id}}"]}""")).GetProperty("plan").GetString()!;
        (await world.CallAsync(ReadWritePlan, $$"""{"plan":"{{plan}}"}""")).GetProperty("applied").GetBoolean().ShouldBeTrue();

        var again = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "decide_theme_merges", $$"""{"project":"SHIP","keep_apart":["{{id}}"]}""");
        var stale = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadWritePlan), "decide_theme_merges", $$"""{"project":"SHIP","plan":"{{plan}}"}""");

        ErrorCode(again).ShouldBe("conflict");
        ErrorCode(stale).ShouldBe("conflict");
    }

    [Test]
    public async Task Preview_WhenBothAcceptAndRejectTheSame_ShouldBeValidation()
    {
        var world = await WorldAsync();
        var (id, _) = await world.ProposedAsync("From", "Into");

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "decide_theme_merges", $$"""{"project":"SHIP","merge":["{{id}}"],"keep_apart":["{{id}}"]}""");

        ErrorCode(reply).ShouldBe("validation");
    }

    [Test]
    public async Task Call_WhenNotAMemberOfTheProject_ShouldBeRefused()
    {
        var world = await WorldAsync();
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Part);
        await seed.ThemeAsync($"Away {world.Tag}");
        await seed.ThemeAsync($"Kept {world.Tag}");
        using (var scope = Host.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRetroDecisionWrites>().ProposeMergeAsync(SeedIds.Projects.Part, $"Away {world.Tag}", $"Kept {world.Tag}", "Same.", Agent);
        }

        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadWritePlan), "decide_theme_merges", """{"project":"PART"}""");

        ErrorCode(reply).ShouldBe("not_found");
    }

    private static string ErrorCode(McpReply reply) => reply.Structured().GetProperty("error").GetProperty("code").GetString()!;

    private async Task<Scenario> WorldAsync() => new(this, await OpenDatabaseAsync());

    private sealed class Scenario(DecideThemeMergesTests tests, Npgsql.NpgsqlConnection connection)
    {
        private readonly RetroSeed _seed = new(connection, SeedIds.Projects.Ship);
        private Guid? _retro;

        public string Tag { get; } = Guid.NewGuid().ToString("N")[..8];

        public string Name(string label) => $"{label} {Tag}";

        public Task<Guid> ThemeAsync(string label) => _seed.ThemeAsync(Name(label));

        public async Task<Guid> PointOnAsync(Guid theme, RetroThemeSetBy setBy)
        {
            if (_retro is null)
            {
                var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").InsertAsync(connection);
                _retro = await _seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, WorkItemId: feature, AgentName: "claude-x"));
            }

            return await _seed.PointAsync(_retro.Value, new RetroSeedPoint(RetroCategory.WentBadly, $"Point {Guid.NewGuid():N}", ThemeId: theme, ThemeSetBy: setBy));
        }

        public async Task PointsOnAsync(Guid theme, int count)
        {
            for (var i = 0; i < count; i++)
            {
                await PointOnAsync(theme, RetroThemeSetBy.Agent);
            }
        }

        public async Task<bool> ProposeAsync(string from, string into)
        {
            using var scope = tests.Host.Services.CreateScope();
            return await scope.ServiceProvider.GetRequiredService<IRetroDecisionWrites>().ProposeMergeAsync(SeedIds.Projects.Ship, Name(from), Name(into), "Same.", Agent);
        }

        public async Task<(Guid Id, (Guid From, Guid Into) Themes)> ProposedAsync(string from, string into)
        {
            var themes = (await ThemeAsync(from), await ThemeAsync(into));
            await ProposeAsync(from, into);
            var id = await connection.QuerySingleAsync<Guid>("SELECT id FROM retro_theme_merge_proposal WHERE from_theme_id = @from", new { from = themes.Item1 });
            return (id, themes);
        }

        public async Task<JsonElement> CallAsync(string[] scopes, string arguments)
        {
            var reply = await tests.CallToolAsync(await tests.TokenAsync(TestUser.Joel, scopes), "decide_theme_merges", $$"""{"project":"SHIP",{{arguments.TrimStart()[1..]}}""".Replace(",}", "}"));
            reply.IsToolError().ShouldBeFalse(reply.Body);
            return reply.Structured();
        }

        public Task<string> StatusAsync(Guid id) => connection.QuerySingleAsync<string>("SELECT status FROM retro_theme_merge_proposal WHERE id = @id", new { id });

        public Task<Guid?> ThemeOfAsync(Guid point) => connection.QuerySingleAsync<Guid?>("SELECT theme_id FROM retro_point WHERE id = @point", new { point });

        public Task<Guid?> MergedIntoAsync(Guid theme) => connection.QuerySingleAsync<Guid?>("SELECT merged_into_id FROM retro_theme WHERE id = @theme", new { theme });
    }
}
