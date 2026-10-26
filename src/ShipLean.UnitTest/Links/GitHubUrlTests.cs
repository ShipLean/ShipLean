using ShipLean.Application.Features.Links.GitHub;
using ShipLean.Contracts.Links;

namespace ShipLean.UnitTest.Links;

[TestFixture]
public class GitHubUrlTests
{
    [TestCase("https://github.com/acme/storefront/pull/482", LinkKind.GithubPull, "repos/acme/storefront/pulls/482")]
    [TestCase("https://github.com/acme/store.front-2/pull/3/files?w=1#diff", LinkKind.GithubPull, "repos/acme/store.front-2/pulls/3")]
    [TestCase("https://GitHub.com/acme/storefront/issues/7", LinkKind.GithubIssue, "repos/acme/storefront/issues/7")]
    public void TryParse_WhenPullOrIssueAddress_ShouldGiveKindAndApiPath(string url, LinkKind kind, string apiPath)
    {
        GitHubUrl.TryParse(new Uri(url), out var parsed).ShouldBeTrue();

        parsed.Kind.ShouldBe(kind);
        parsed.ApiPath.ShouldBe(apiPath);
    }

    [Test]
    public void TryParse_WhenPullAddress_ShouldGiveNumberAndLocation()
    {
        GitHubUrl.TryParse(new Uri("https://github.com/acme/storefront/pull/482"), out var parsed).ShouldBeTrue();

        parsed.Number.ShouldBe(482);
        parsed.Location.ShouldBe("acme/storefront");
    }

    [TestCase("https://github.com/acme/storefront")]
    [TestCase("https://github.com/acme/storefront/pull/new")]
    [TestCase("https://github.com/acme/storefront/pull/0")]
    [TestCase("https://github.com/acme/storefront/pulls")]
    [TestCase("https://github.com/acme/storefront/commit/482")]
    [TestCase("https://github.com/../storefront/pull/1")]
    [TestCase("https://github.com/acme/%2e%2e/pull/1")]
    [TestCase("https://github.com/acme/a%2Fb/pull/1")]
    [TestCase("https://gist.github.com/acme/storefront/pull/1")]
    [TestCase("https://example.com/acme/storefront/pull/1")]
    public void TryParse_WhenNotAPullOrIssue_ShouldBeFalse(string url)
    {
        GitHubUrl.TryParse(new Uri(url), out _).ShouldBeFalse();
    }
}
