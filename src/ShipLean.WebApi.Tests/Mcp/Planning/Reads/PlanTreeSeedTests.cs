using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Application.Features.Mcp.Planning.Reads;
using ShipLean.Contracts.Attachments;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Plans;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Reads;

// PlanTreeSeed, checked by reading what it wrote back through IPlanReads.
[TestFixture]
public class PlanTreeSeedTests : ApiFixture
{
    [Test]
    public async Task PlanTreeSeed_FeatureEditorFixture_ShouldReadBackAsTree()
    {
        var plan = PlanFixture.FeatureEditor;
        Host.ActAs(SignedInAs.Id);

        var keys = await PlanTreeSeed.SeedAsync(Host.Services, SeedIds.Projects.Ship, plan);

        using var scope = Host.Services.CreateScope();
        Guid featureId;
        await using (var connection = await OpenDatabaseAsync())
        {
            featureId = await connection.QuerySingleAsync<Guid>("SELECT id FROM work_item WHERE key = @key", new { key = keys.Feature });
        }
        var tree = await scope.ServiceProvider.GetRequiredService<IPlanReads>().TreeAsync(featureId);

        tree.Feature.Title.ShouldBe(plan.Feature.Title);
        tree.Feature.SharedBranch.ShouldBe(plan.SharedBranch);
        tree.Feature.Goal.ShouldBe(plan.Feature.Goal);
        tree.Feature.Description.ShouldBe(plan.Feature.Description);
        MarkdownSections.Extract(tree.Feature.Description, PlanHeadings.Contracts).ShouldNotBeNull();
        tree.Stories.Select(s => (s.Title, s.Wave, s.Lane)).ShouldBe(plan.Stories.Select(s => (s.Title, s.Wave, s.Lane)));
        tree.Stories.Select(s => s.Key).ShouldBe(plan.Stories.Select(s => keys.Stories[s.Title]));
        foreach (var (story, expected) in tree.Stories.Zip(plan.Stories))
        {
            story.Tasks.Select(t => t.Title).ShouldBe(expected.Tasks.Select(t => t.Title));
            story.Tasks.Select(t => t.Description).ShouldBe(expected.Tasks.Select(t => t.Brief));
            story.Tasks.Select(t => t.Key).ShouldBe(expected.Tasks.Select(t => keys.Tasks[t.Title]));
            story.Tasks.Select(t => t.Tags).ShouldBe(expected.Tasks.Select(t => t.Tags));
            story.Tasks.ShouldAllBe(t => !t.Orphan);
        }
        keys.Tasks.Count.ShouldBe(plan.Stories.Sum(s => s.Tasks.Count));

        var store = scope.ServiceProvider.GetRequiredService<IAttachmentStore>();
        var attachments = await store.ListAsync(featureId);
        attachments.Select(a => a.Name).ShouldBe([PlanFiles.CommonBrief, PlanFiles.Plan], ignoreOrder: true);
        var attached = attachments.Single(a => a.Name == PlanFiles.CommonBrief);
        attached.ContentType.ShouldBe("text/markdown");
        (await ReadAsync(store, attached.Id)).ShouldBe(plan.CommonBrief);
        (await ReadAsync(store, attachments.Single(a => a.Name == PlanFiles.Plan).Id)).ShouldBe(plan.PlanMarkdown);

        keys.Ids.Count.ShouldBe(1 + plan.Stories.Count + keys.Tasks.Count);
        keys.Ids[keys.Feature].ShouldBe(featureId);
        tree.Stories.ShouldAllBe(s => keys.Ids[s.Key] == s.Id);
        tree.Stories.SelectMany(s => s.Tasks).ShouldAllBe(t => keys.Ids[t.Key] == t.Id);
    }

    private static async Task<string> ReadAsync(IAttachmentStore store, Guid id)
    {
        var (_, content) = await store.OpenAsync(id);
        await using var stream = content.Content;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return await reader.ReadToEndAsync();
    }
}
