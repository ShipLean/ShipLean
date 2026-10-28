using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Contracts.Attachments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.WebApi.Features.Mcp.Planning;
using ShipLean.WebApi.Features.Mcp.Planning.Briefs;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Briefs;

// get_brief against the seeded Feature Editor plan, as Joel.
[TestFixture]
public class GetBriefTests : McpFixture
{
    private PlanSeedKeys _keys = null!;

    [OneTimeSetUp]
    public async Task SeedPlanAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        _keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
    }

    private async Task<JsonElement> BriefAsync(string item)
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var reply = await CallToolAsync(token, "get_brief", $$"""{"item":"{{item}}"}""");
        reply.IsToolError().ShouldBeFalse();
        return reply.Structured();
    }

    private async Task<string> StateNameAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<string>(
            "SELECT s.name FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.key = @key", new { key });
    }

    [Test]
    public async Task GetBrief_WhenClientReadsOnlyText_ShouldGiveTheSummaryThenTheMarkdownThenTheJson()
    {
        var task = _keys.Tasks.Values.First();
        var reply = await CallToolAsync(await TokenAsync(TestUser.Joel, ReadOnly), "get_brief", $$"""{"item":"{{task}}"}""");

        var texts = reply.Texts();
        texts.Count.ShouldBe(3);
        texts[0].ShouldBe($"The brief for {task}; the Markdown follows.");
        texts[1].ShouldBe(reply.Structured().GetProperty("markdown").GetString());
        JsonElement.DeepEquals(reply.TextJson(), reply.Structured()).ShouldBeTrue();
    }

    [Test]
    public async Task GetBrief_Task_ShouldFillEveryBriefPlaceholder()
    {
        var plan = PlanFixture.FeatureEditor;
        var task = plan.Stories.SelectMany(story => story.Tasks).First(candidate => candidate.Title.StartsWith("F1:", StringComparison.Ordinal));

        var brief = await BriefAsync(_keys.Tasks[task.Title]);

        brief.GetProperty("kind").GetString().ShouldBe("task");
        brief.GetProperty("item").GetString().ShouldBe(_keys.Tasks[task.Title]);
        brief.GetProperty("waves").ValueKind.ShouldBe(JsonValueKind.Null);
        var markdown = brief.GetProperty("markdown").GetString()!;
        markdown.ShouldStartWith(BriefPlanContext.StateRule);
        markdown.ShouldNotContain("{{");
        var order = new[] { plan.CommonBrief.Trim(), _keys.Tasks[task.Title], task.Title, task.Brief.Trim(), plan.SharedBranch }
            .Select(part => markdown.IndexOf(part, StringComparison.Ordinal)).ToList();
        order.ShouldAllBe(index => index >= 0);
        order.ShouldBe(order.Order().ToList());
        markdown.IndexOf(PlanGuide.Read(PlanGuidePart.Report).Trim(), order[^1], StringComparison.Ordinal)
            .ShouldBeGreaterThan(order[^1]);
    }

    [Test]
    public async Task GetBrief_Story_ShouldListItsTasksAndTheContractsSection()
    {
        var plan = PlanFixture.FeatureEditor;
        var story = plan.Stories.First(candidate => candidate.Lane == "F");

        var brief = await BriefAsync(_keys.Stories[story.Title]);

        brief.GetProperty("kind").GetString().ShouldBe("story");
        var markdown = brief.GetProperty("markdown").GetString()!;
        markdown.ShouldNotContain("{{");
        foreach (var task in story.Tasks)
        {
            var key = _keys.Tasks[task.Title];
            markdown.ShouldContain($"| {key} | {task.Title} | {task.Model} | {task.ContextK}k | {await StateNameAsync(key)} |");
        }
        var storyKey = _keys.Stories[story.Title];
        var storyState = await StateNameAsync(storyKey);
        brief.GetProperty("item").GetString().ShouldBe(storyKey);
        markdown.IndexOf(_keys.Feature, StringComparison.Ordinal).ShouldBeInRange(0, markdown.IndexOf(storyKey, StringComparison.Ordinal));
        // The template lines naming the keys, wave, lane and branch, filled the way the guide's template reads them.
        var identityLines = PlanGuide.Read(PlanGuidePart.Lead).ReplaceLineEndings("\n").Split('\n')
            .Where(line => new[] { PlanPlaceholders.FeatureKey, PlanPlaceholders.StoryKey, PlanPlaceholders.Wave, PlanPlaceholders.Lane, PlanPlaceholders.SharedBranch }
                .Any(placeholder => line.Contains(placeholder, StringComparison.Ordinal)))
            .Select(line => PlanTemplate.Fill(line, new Dictionary<string, string>
            {
                [PlanPlaceholders.FeatureKey] = _keys.Feature,
                [PlanPlaceholders.StoryKey] = storyKey,
                [PlanPlaceholders.Wave] = story.Wave.Text,
                [PlanPlaceholders.Lane] = story.Lane!,
                [PlanPlaceholders.SharedBranch] = plan.SharedBranch,
            }));
        identityLines.ShouldNotBeEmpty();
        foreach (var line in identityLines)
        {
            markdown.ShouldContain(line);
        }
        storyState.ShouldNotBeNullOrEmpty();
        var contracts = MarkdownSections.Extract(
            plan.Feature.Description, PlanHeadings.Contracts)!;
        markdown.ShouldContain(contracts);
    }

    [Test]
    public async Task GetBrief_Feature_ShouldGroupByWaveAndNameStartableWave()
    {
        var plan = PlanFixture.FeatureEditor;

        var brief = await BriefAsync(_keys.Feature);

        brief.GetProperty("kind").GetString().ShouldBe("feature");
        var waves = brief.GetProperty("waves").EnumerateArray().ToList();
        waves.Select(wave => wave.GetProperty("wave").GetString()).ShouldBe(["0.1", "0.2", "1", "2"]);
        waves.Sum(wave => wave.GetProperty("stories").GetArrayLength()).ShouldBe(plan.Stories.Count);
        waves.ShouldAllBe(wave => !wave.GetProperty("done").GetBoolean());
        brief.GetProperty("startable_wave").GetString().ShouldBe("0.1");
        var markdown = brief.GetProperty("markdown").GetString()!;
        markdown.ShouldStartWith(BriefPlanContext.StateRule);
        markdown.ShouldContain($"# {_keys.Feature}: {plan.Feature.Title}");
        markdown.ShouldContain(plan.Feature.Goal);
        markdown.ShouldNotContain("(done)");
    }

    [Test]
    public async Task GetBrief_FeatureWithWaveDone_ShouldMoveStartableWaveOn()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var plan = PlanFixture.FeatureEditor;
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, plan);
        var firstWave = plan.Stories.Where(story => story.Wave.Text == "0.1").Select(story => keys.Stories[story.Title]).ToList();
        firstWave.ShouldNotBeEmpty();
        string completedName;
        await using (var connection = await OpenDatabaseAsync())
        {
            var completed = await connection.QueryFirstAsync<(Guid Id, string Name)>(
                "SELECT id, name FROM state WHERE project_id = @project AND type = 'story' AND category = 'completed' AND archived_at IS NULL ORDER BY name LIMIT 1",
                new { project = SeedIds.Projects.Ship });
            completedName = completed.Name;
            await connection.ExecuteAsync("UPDATE work_item SET state_id = @id WHERE key = ANY(@keys)", new { completed.Id, keys = firstWave.ToArray() });
        }

        var brief = await BriefAsync(keys.Feature);

        brief.GetProperty("startable_wave").GetString().ShouldBe("0.2");
        var waves = brief.GetProperty("waves").EnumerateArray().ToList();
        waves[0].GetProperty("done").GetBoolean().ShouldBeTrue();
        waves[1].GetProperty("done").GetBoolean().ShouldBeFalse();
        var stories = waves[0].GetProperty("stories").EnumerateArray().ToList();
        stories.Select(story => story.GetProperty("key").GetString()).ShouldBe(firstWave, ignoreOrder: true);
        stories.ShouldAllBe(story => story.GetProperty("done").GetBoolean() && story.GetProperty("state").GetString() == completedName);
        waves[1].GetProperty("stories").EnumerateArray().ShouldAllBe(story => !story.GetProperty("done").GetBoolean());
        var markdown = brief.GetProperty("markdown").GetString()!;
        markdown.ShouldContain("## Wave 0.1 (done)");
        markdown.ShouldContain("Startable wave: 0.2");
        markdown.ShouldNotContain("## Wave 0.2 (done)");
    }

    [Test]
    public async Task GetBrief_TaskWithoutCommonBrief_ShouldSaySoInTheBrief()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        await using var scope = Host.Services.CreateAsyncScope();
        var attachments = scope.ServiceProvider.GetRequiredService<IAttachmentStore>();
        var common = (await attachments.ListAsync(keys.Ids[keys.Feature])).Single(file => file.Name == PlanFiles.CommonBrief);
        await attachments.ArchiveAsync(common.Id);
        var title = PlanFixture.FeatureEditor.Stories[0].Tasks[0].Title;

        var brief = await BriefAsync(keys.Tasks[title]);

        brief.GetProperty("markdown").GetString()!.ShouldContain(BriefMarkdown.NoCommonBrief);
    }

    [Test]
    public async Task GetBrief_ItemOutsideAPlan_ShouldBeValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        await using var connection = await OpenDatabaseAsync();
        var plain = await connection.QueryFirstAsync<string>(
            "SELECT key FROM work_item WHERE project_id = @project AND parent_id IS NULL AND key NOT IN (SELECT key FROM work_item WHERE title = @feature) ORDER BY key LIMIT 1",
            new { project = SeedIds.Projects.Ship, feature = PlanFixture.FeatureEditor.Feature.Title });

        var reply = await CallToolAsync(token, "get_brief", $$"""{"item":"{{plain}}"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe("validation");
        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("item");
    }
}
