using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Attachments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Plans;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Guide;

// A retro attachment whose bytes can no longer be opened (here its blob reference points at nothing, as when it is archived or
// replaced between the lessons query and the open) is skipped, and the next retro takes its place. The call still succeeds.
[TestFixture]
public class PlanningGuideUnreadableRetroTests : McpFixture
{
    private const string Section = "## What to change next time\n\n";

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task SeedRetrosAsync()
    {
        Host.ActAs(SeedIds.Users.Joel);
        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, PlanFixture.FeatureEditor);
        using var scope = Host.Services.CreateScope();
        var attachments = scope.ServiceProvider.GetRequiredService<IAttachmentStore>();
        var id = keys.Ids[keys.Feature];

        await Attach(attachments, id, "retro-2026-01-01-readable.md", "# Retro\n\n" + Section + "Readable lesson.");
        await Attach(attachments, id, "retro-2026-03-01-unreadable.md", "# Retro\n\n" + Section + "Unreadable lesson.");
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync("UPDATE attachment SET storage_ref = 'gone/nothing' WHERE name = 'retro-2026-03-01-unreadable.md'");
    }

    [Test]
    public async Task PlanningGuide_WhenARetroCannotBeOpened_ShouldSkipItAndTakeTheNext()
    {
        var token = await TokenAsync(TestUser.Joel, ReadOnly);

        var reply = await CallToolAsync(token, "planning_guide", """{"project":"SHIP"}""");

        reply.IsToolError().ShouldBeFalse();
        var lessons = reply.Structured().GetProperty("lessons").EnumerateArray().ToList();
        lessons.Select(l => l.GetProperty("text").GetString()).ShouldBe(["Readable lesson."]);
    }

    private static async Task Attach(IAttachmentStore attachments, Guid itemId, string name, string markdown)
    {
        await using var content = new MemoryStream(Encoding.UTF8.GetBytes(markdown));
        await attachments.AddAsync(itemId, name, "text/markdown", content);
    }
}
