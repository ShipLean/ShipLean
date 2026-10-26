using ShipLean.Application.Features.Links.Rows;
using ShipLean.Contracts.Links;

namespace ShipLean.UnitTest.Links;

[TestFixture]
public class LinkAddressTests
{
    [TestCase("https://github.com/acme/storefront/pull/482", LinkKind.GithubPull, 482, "acme/storefront", "github.com")]
    [TestCase("https://github.com/acme/storefront/issues/7?x=1#top", LinkKind.GithubIssue, 7, "acme/storefront", "github.com")]
    [TestCase("https://gitlab.com/payments/vault/-/merge_requests/31", LinkKind.GitlabMergeRequest, 31, "payments/vault", "gitlab.com")]
    [TestCase("https://gitlab.example.com/payments/team/vault/-/issues/7", LinkKind.GitlabIssue, 7, "gitlab.example.com · payments/team/vault", "gitlab.example.com")]
    public void Of_WhenAddressNamesAPullRequestMergeRequestOrIssue_ShouldReadItsKindNumberAndLocation(string url, LinkKind kind, int number, string location, string host)
    {
        var address = LinkAddress.Of(url);

        address.Kind.ShouldBe(kind);
        address.Number.ShouldBe(number);
        address.Location.ShouldBe(location);
        address.Host.ShouldBe(host);
        address.Group.ShouldBe(LinkGroup.Code);
    }

    [TestCase("https://github.com/acme/storefront")]
    [TestCase("https://github.com/acme/storefront/pull/new")]
    [TestCase("https://github.com/acme/storefront/pull/0")]
    [TestCase("https://github.com/acme/storefront/pulls")]
    [TestCase("https://gitlab.example.com/payments/vault/-/pipelines/5")]
    [TestCase("https://gitlab.example.com/-/merge_requests/5")]
    [TestCase("https://wiki.example.com/pull/12")]
    public void Of_WhenAddressIsAnythingElse_ShouldBeAPage(string url)
    {
        var address = LinkAddress.Of(url);

        address.Kind.ShouldBe(LinkKind.Page);
        address.Group.ShouldBe(LinkGroup.Page);
        address.Number.ShouldBeNull();
        address.Location.ShouldBeNull();
    }

    [TestCase("not a url")]
    [TestCase("ftp://github.com/acme/storefront/pull/1")]
    [TestCase("")]
    public void Of_WhenNotAnAbsoluteHttpAddress_ShouldBeAPageWithNoHost(string url)
    {
        var address = LinkAddress.Of(url);

        address.Kind.ShouldBe(LinkKind.Page);
        address.Host.ShouldBeEmpty();
    }
}
