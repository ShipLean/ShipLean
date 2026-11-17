using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.InspectionMetrics.Quality;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.InspectionMetrics;

// More cases of the real quality facts reader (QK4), on rows written in SQL: merged PRs that are not linked to the project, a bug
// in another project, and a PR linked to two items of the project.
[TestFixture]
public class QualityFactsReaderTests : ApiFixture
{
    private const string LinkedUrl = "https://github.com/acme/ship/pull/21";
    private const string UnlinkedUrl = "https://github.com/acme/ship/pull/22";
    private const string OtherProjectUrl = "https://github.com/acme/ship/pull/23";

    private static readonly DateTimeOffset T0 = new(2026, 10, 6, 9, 0, 0, TimeSpan.Zero);

    private string _otherBug = string.Empty;
    private BugFacts _facts = null!;

    [OneTimeSetUp]
    public async Task Arrange()
    {
        await using var connection = await OpenDatabaseAsync();
        var first = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("First").WithCreatedAt(T0).InsertAsync(connection);
        var second = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("task").WithTitle("Second").WithCreatedAt(T0).InsertAsync(connection);
        var other = await new WorkItemBuilder(SeedIds.Projects.Pay).WithType("task").WithTitle("Elsewhere").WithCreatedAt(T0).InsertAsync(connection);
        var otherBug = await new WorkItemBuilder(SeedIds.Projects.Pay).WithType("bug").WithTitle("Another project's bug").WithCreatedAt(T0).InsertAsync(connection);
        foreach (var (item, url) in new[] { (first, LinkedUrl), (second, LinkedUrl), (other, OtherProjectUrl) })
        {
            await connection.ExecuteAsync(
                "INSERT INTO work_item_link (id, source_id, link_type, url, title) VALUES (gen_random_uuid(), @item, 'url', @url, 'PR')", new { item, url });
        }
        foreach (var url in new[] { LinkedUrl, UnlinkedUrl, OtherProjectUrl })
        {
            await connection.ExecuteAsync(
                """
                INSERT INTO link_state (url_key, url, state, as_of, source, merged_at)
                VALUES (link_url_key(@url), @url, 'merged', @at, 'github', @at)
                """, new { url, at = T0.AddDays(1) });
        }
        _otherBug = await connection.ExecuteScalarAsync<string>("SELECT key FROM work_item WHERE id = @otherBug", new { otherBug }) ?? string.Empty;

        await using var scope = Host.Services.CreateAsyncScope();
        _facts = await scope.ServiceProvider.GetRequiredService<IQualityFactsReader>()
            .ReadAsync(SeedIds.Projects.Ship, new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 16), CancellationToken.None);
    }

    // A merged PR with no live link to an item of the project is not the project's work. Mutation: count all link_state rows.
    [Test]
    public void Read_WhenPrUnlinked_ShouldNotCountIt()
    {
        _facts.MergedPrs.ShouldNotContain(pr => pr.Url == UnlinkedUrl);
    }

    // Mutation: drop the project filter on the PR's linked item.
    [Test]
    public void Read_WhenPrLinkedOnlyInAnotherProject_ShouldNotCountIt()
    {
        _facts.MergedPrs.ShouldNotContain(pr => pr.Url == OtherProjectUrl);
    }

    // A PR linked to two items of the project is one PR. Mutation: join the links instead of EXISTS.
    [Test]
    public void Read_WhenPrLinkedToTwoItems_ShouldCountItOnce()
    {
        _facts.MergedPrs.Count(pr => pr.Url == LinkedUrl).ShouldBe(1);
    }

    // Mutation: drop the project filter on bugs.
    [Test]
    public void Read_WhenBugInAnotherProject_ShouldNotReturnIt()
    {
        _facts.Bugs.ShouldNotContain(bug => bug.Key == _otherBug);
    }
}
