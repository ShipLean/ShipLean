using Dapper;
using ShipLean.TestSupport.Webhooks;

namespace ShipLean.WebApi.Tests.Webhooks;

// A merged GitLab merge request stores its merge time and merge commit on the link state, as a merged GitHub pull request does.
[TestFixture]
public class GitLabMergedAtTests : WebhookApiTest
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?> { [WebhookSecrets.GitlabKey] = GitlabSecret };

    [Test]
    public async Task Hook_WhenMergeRequestMerged_ShouldStoreMergedAt()
    {
        var story = await CreateAsync("story", "Fixed by a GitLab merge");
        await LinkAsync(story.Id, WebhookDelivery.GitlabUrl(61));
        var mergedAt = Next();
        var updatedAt = Next();

        await DeliverAsync(WebhookDelivery.Gitlab(GitlabSecret, "merge", 61, "Fix it", updatedAt, state: "merged", mergedAt: mergedAt, mergeCommitSha: "6DCB09B5B57875F334F61AEBED695E2E4193DB5E"));

        (await MergeAsync(61)).ShouldBe(("6dcb09b5b57875f334f61aebed695e2e4193db5e", mergedAt));
    }

    [Test]
    public async Task Hook_WhenMergeRequestOpen_ShouldStoreNoMergedAt()
    {
        var story = await CreateAsync("story", "Still open on GitLab");
        await LinkAsync(story.Id, WebhookDelivery.GitlabUrl(62));

        await DeliverAsync(WebhookDelivery.Gitlab(GitlabSecret, "open", 62, "Fix it", Next(), mergeCommitSha: "1111111111111111111111111111111111111111"));

        (await MergeAsync(62)).ShouldBe((null, null));
    }

    private async Task<(string? Sha, DateTimeOffset? MergedAt)> MergeAsync(int iid)
    {
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(string? Sha, DateTime? MergedAt)>(
            "SELECT merge_commit_sha, merged_at FROM link_state WHERE url_key = link_url_key(@url)", new { url = WebhookDelivery.GitlabUrl(iid) });
        return (row.Sha, row.MergedAt is { } at ? new DateTimeOffset(at, TimeSpan.Zero) : null);
    }
}
