using ShipLean.Application.Features.Links.GitLab;
using ShipLean.Contracts.Links;

namespace ShipLean.UnitTest.Links;

[TestFixture]
public class GitLabUrlTests
{
    private const string Root = "https://gitlab.example.com";

    [TestCase("https://gitlab.example.com/payments/vault/-/merge_requests/31", LinkKind.GitlabMergeRequest, "payments/vault", "api/v4/projects/payments%2Fvault/merge_requests/31")]
    [TestCase("https://gitlab.example.com/payments/team/vault/-/issues/7?x=1#note", LinkKind.GitlabIssue, "payments/team/vault", "api/v4/projects/payments%2Fteam%2Fvault/issues/7")]
    [TestCase("https://GitLab.Example.com/payments/vault/-/merge_requests/31/diffs", LinkKind.GitlabMergeRequest, "payments/vault", "api/v4/projects/payments%2Fvault/merge_requests/31")]
    public void TryParse_WhenMergeRequestOrIssueAddress_ShouldGiveKindProjectAndApiPath(string url, LinkKind kind, string project, string apiPath)
    {
        GitLabUrl.TryParse(new Uri(url), Root, out var parsed).ShouldBeTrue();

        parsed.Kind.ShouldBe(kind);
        parsed.ProjectPath.ShouldBe(project);
        parsed.ApiPath.ShouldBe(apiPath);
    }

    [Test]
    public void TryParse_WhenMergeRequestAddress_ShouldGiveNumberAndLocation()
    {
        GitLabUrl.TryParse(new Uri("https://gitlab.example.com/payments/vault/-/merge_requests/31"), Root, out var parsed).ShouldBeTrue();

        parsed.Number.ShouldBe(31);
        parsed.Location.ShouldBe("gitlab.example.com · payments/vault");
    }

    [Test]
    public void TryParse_WhenGitLabUrlHasAPathPrefix_ShouldStripItFromTheProject()
    {
        GitLabUrl.TryParse(new Uri("https://corp.example.com/gitlab/payments/vault/-/issues/2"), "https://corp.example.com/gitlab/", out var parsed).ShouldBeTrue();

        parsed.ProjectPath.ShouldBe("payments/vault");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    [TestCase("not a url")]
    public void TryParse_WhenGitLabUrlNotConfigured_ShouldBeFalse(string? gitLabUrl)
    {
        GitLabUrl.TryParse(new Uri("https://gitlab.example.com/payments/vault/-/merge_requests/31"), gitLabUrl, out _).ShouldBeFalse();
    }

    [TestCase("https://gitlab.other.example/payments/vault/-/merge_requests/31")]
    [TestCase("https://gitlab.example.com/payments/vault/-/pipelines/5")]
    [TestCase("https://gitlab.example.com/payments/vault/-/merge_requests/0")]
    [TestCase("https://gitlab.example.com/payments/vault/-/merge_requests/new")]
    [TestCase("https://gitlab.example.com/vault/-/merge_requests/5")]
    [TestCase("https://gitlab.example.com/-/merge_requests/5")]
    [TestCase("https://gitlab.example.com/payments/../vault/-/merge_requests/5")]
    [TestCase("https://gitlab.example.com/payments/va%2Fult/-/merge_requests/5")]
    [TestCase("ftp://gitlab.example.com/payments/vault/-/merge_requests/5")]
    public void TryParse_WhenNotAMergeRequestOrIssueOnTheConfiguredGitLab_ShouldBeFalse(string url)
    {
        GitLabUrl.TryParse(new Uri(url), Root, out _).ShouldBeFalse();
    }
}
