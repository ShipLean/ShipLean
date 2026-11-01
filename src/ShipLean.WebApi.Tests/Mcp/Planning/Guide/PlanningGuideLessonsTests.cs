using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Retros;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.TestSupport.Retros;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Guide;

// planning_guide lessons: the three newest retros of SHIP's live Features, attachments and comments together. Created oldest to newest:
// an old retro, a comment, a superseded comment, a file that is not a retro, a retro that is then replaced (the old version is archived),
// an archived comment, the new version, one with no section, and the newest retro. After those come retros that must not count: the
// newest of all, on a PART Feature, on a SHIP Story and on a Removed SHIP Feature, so each would make the cut if its rule leaked.
// Every live filter has one candidate that ranks in the top three if the filter is dropped, and one at a time.
[TestFixture]
public class PlanningGuideLessonsTests : McpFixture
{
    private const string Section = "## What to change next time\n\n";

    protected override bool WithProbeTools => false;

    private string _feature = null!;

    [OneTimeSetUp]
    public async Task SeedRetrosAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        var part = await SeedPartFeatureAsync();
        var dropped = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        _feature = keys.Feature;
        using var scope = Host.Services.CreateScope();
        var attachments = scope.ServiceProvider.GetRequiredService<IAttachmentStore>();
        var comments = scope.ServiceProvider.GetRequiredService<ICommentStore>();
        var id = keys.Ids[keys.Feature];

        await Attach(attachments, id, "retro-2026-01-01-oldest.md", "# Retro\n\n" + Section + "Oldest lesson.");
        await comments.AddAsync(id, new CommentCreate("Retro · standup\n\n" + Section + "Comment lesson."));
        var stale = await comments.AddAsync(id, new CommentCreate("Retro · stale\n\n" + Section + "Superseded lesson."));
        await comments.SupersedeAsync(stale.Id);
        await Attach(attachments, id, "notes.md", "# Notes\n\n" + Section + "Not a retro lesson.");
        await Attach(attachments, id, "retro-2026-02-01-replaced.md", "# Retro\n\n" + Section + "First draft lesson.");
        var withdrawn = await comments.AddAsync(id, new CommentCreate("Retro · withdrawn\n\n" + Section + "Archived comment lesson."));
        await comments.ArchiveAsync(withdrawn.Id);
        await Attach(attachments, id, "retro-2026-02-01-replaced.md", "# Retro\n\n" + Section + "Second draft lesson.\n\n## Other\n\nNot a lesson.");
        await Attach(attachments, id, "retro-2026-02-02-bare.md", "# Retro\n\nNothing to change.");
        await Attach(attachments, id, "retro-2026-03-01-newest.md", "# Retro\n\n" + Section + "Newest lesson.");

        // None of these may appear: another project, a Story rather than a Feature, and a Feature the team removed.
        await using (var connection = await OpenDatabaseAsync())
        {
            await Attach(attachments, part, "retro-2026-04-01-other-project.md", "# Retro\n\n" + Section + "Other project lesson.");
            await connection.ExecuteAsync(
                "UPDATE work_item SET state_id = (SELECT id FROM state WHERE project_id = @project AND type = 'feature' AND category = 'removed' LIMIT 1) WHERE id = @id",
                new { project = SeedIds.Projects.Ship, id = dropped.Ids[dropped.Feature] });
        }
        await comments.AddAsync(keys.Ids[keys.Stories.Values.First()], new CommentCreate("Retro · story\n\n" + Section + "Story lesson."));
        await Attach(attachments, dropped.Ids[dropped.Feature], "retro-2026-04-02-removed.md", "# Retro\n\n" + Section + "Removed feature file lesson.");
        await comments.AddAsync(dropped.Ids[dropped.Feature], new CommentCreate("Retro · removed\n\n" + Section + "Removed feature comment lesson."));
    }

    [Test]
    public async Task PlanningGuide_WithProject_ShouldIncludeLessonsFromNewestRetroCommentsAndAttachments()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "planning_guide", """{"project":"SHIP"}""");

        reply.IsToolError().ShouldBeFalse();
        var lessons = reply.Structured().GetProperty("lessons").EnumerateArray().ToList();
        lessons.Select(l => l.GetProperty("feature").GetString()).ShouldBe([_feature, _feature, _feature]);
        lessons.Select(l => l.GetProperty("text").GetString()).ShouldBe(["Newest lesson.", "Second draft lesson.", "Comment lesson."]);
    }

    [Test]
    public async Task Lessons_WhenConductorRetroHasImprovePoints_ShouldIncludeThem()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        var retro = await SeedRetroAsync(keys.Ids[keys.Feature], RetroRole.Conductor, ("Give every agent search access.", false), ("Ship the schema first.", false));

        var lessons = await LessonsAsync();

        await ArchiveRetroAsync(retro);
        var improve = lessons.Where(l => l.Retro.StartsWith("retrospective ", StringComparison.Ordinal)).ToList();
        improve.Select(l => l.Feature).ShouldBe([keys.Feature]);
        improve[0].Text.ShouldBe("- Give every agent search access.\n- Ship the schema first.");
        lessons.Take(3).Select(l => l.Text).ShouldBe(["Newest lesson.", "Second draft lesson.", "Comment lesson."]);
    }

    [Test]
    public async Task Lessons_WhenOnlySubAgentRetros_ShouldNotInclude()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        var retro = await SeedRetroAsync(keys.Ids[keys.Feature], RetroRole.SubAgent, ("A sub-agent improve point.", false));

        var lessons = await LessonsAsync();

        await ArchiveRetroAsync(retro);
        lessons.ShouldNotContain(l => l.Text.Contains("A sub-agent improve point.", StringComparison.Ordinal));
    }

    [Test]
    public async Task Lessons_WhenArchivedPoint_ShouldNotInclude()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        var retro = await SeedRetroAsync(keys.Ids[keys.Feature], RetroRole.Conductor, ("A kept point.", false), ("An archived point.", true));

        var lessons = await LessonsAsync();

        await ArchiveRetroAsync(retro);
        var text = string.Join("\n", lessons.Select(l => l.Text));
        text.ShouldContain("A kept point.");
        text.ShouldNotContain("An archived point.");
    }

    private async Task<List<(string Feature, string Retro, string Text)>> LessonsAsync()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);
        var reply = await CallToolAsync(token, "planning_guide", """{"project":"SHIP"}""");
        reply.IsToolError().ShouldBeFalse();
        return [.. reply.Structured().GetProperty("lessons").EnumerateArray()
            .Select(l => (l.GetProperty("feature").GetString()!, l.GetProperty("retro").GetString()!, l.GetProperty("text").GetString()!))];
    }

    // The retro is archived again by the test, so the shared project's lessons are what the other cases expect.
    private async Task<Guid> SeedRetroAsync(Guid featureId, RetroRole role, params (string Text, bool Archived)[] improve)
    {
        await using var connection = await OpenDatabaseAsync();
        var seed = new RetroSeed(connection, SeedIds.Projects.Ship);
        var retro = await seed.RetroAsync(new RetroSeedRetro(SeedIds.Users.Joel, role, WorkItemId: featureId, AgentName: "lessons-test", CreatedAt: new DateTimeOffset(2099, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        foreach (var (text, archived) in improve)
        {
            await seed.PointAsync(retro, new RetroSeedPoint(RetroCategory.Improve, text, Archived: archived));
        }

        return retro;
    }

    private async Task ArchiveRetroAsync(Guid retroId)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE retrospective SET archived_at = now() WHERE id = @retroId", new { retroId });
    }

    // Joel is not in PART, so he joins for the seed and the membership is archived again, which leaves a PART Feature with a retro.
    private async Task<Guid> SeedPartFeatureAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id, role, created_at, updated_at) VALUES (@id, @project, @user, 'admin', now(), now())",
            new { id = Guid.CreateVersion7(), project = SeedIds.Projects.Part, user = SeedIds.Users.Joel });
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Part, PlanFixture.FeatureEditor);
        await connection.ExecuteAsync(
            "UPDATE project_member SET archived_at = now() WHERE project_id = @project AND user_id = @user",
            new { project = SeedIds.Projects.Part, user = SeedIds.Users.Joel });
        return keys.Ids[keys.Feature];
    }

    private static async Task Attach(IAttachmentStore attachments, Guid itemId, string name, string markdown)
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        await attachments.AddAsync(itemId, name, "text/markdown", content);
    }
}
