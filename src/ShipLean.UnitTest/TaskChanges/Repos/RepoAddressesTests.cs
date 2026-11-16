using ShipLean.Application.Features.TaskChanges.Repos;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.UnitTest.TaskChanges.Repos;

[TestFixture]
public class RepoAddressesTests
{
    private const string Sha = "0123456789abcdef0123456789abcdef01234567";

    private static readonly RepoAddresses Addresses = new(new Settings());

    [TestCase("git@github.com:acme/storefront.git")]
    [TestCase("https://github.com/acme/storefront.git")]
    [TestCase("https://github.com/acme/storefront")]
    [TestCase("ssh://git@github.com/acme/storefront")]
    [TestCase("github.com/acme/storefront")]
    [TestCase("https://GitHub.com/ACME/Storefront.git")]
    public async Task RepoAddresses_WhenGivenEachRemoteForm_ShouldParseSameRepo(string remote)
    {
        var repo = await Addresses.ParseAsync(remote);

        repo.ShouldBe(new RepoRef(CodeProvider.Github, "github.com", "acme/storefront"));
    }

    [Test]
    public async Task RepoAddresses_WhenGitLabSubgroup_ShouldKeepTheWholePath()
    {
        var repo = await Addresses.ParseAsync("git@gitlab.example.com:Team/Sub/App.git");

        repo.ShouldBe(new RepoRef(CodeProvider.Gitlab, "gitlab.example.com", "team/sub/app"));
    }

    [Test]
    public async Task RepoAddresses_WhenHostUnknown_ShouldThrowRepoUnreadable()
    {
        var parse = async () => await Addresses.ParseAsync("https://bitbucket.org/acme/storefront");

        (await parse.ShouldThrowAsync<ApiException>()).Code.ShouldBe(ApiErrorCodes.RepoUnreadable);
    }

    [Test]
    public async Task RepoAddresses_WhenNotARepository_ShouldThrowValidationOnRepo()
    {
        var parse = async () => await Addresses.ParseAsync("github.com/acme");

        var error = await parse.ShouldThrowAsync<ApiException>();
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("repo");
    }

    [TestCase("https://github.com/acme/storefront/commit/" + Sha, "github.com", "acme/storefront")]
    [TestCase("https://gitlab.example.com/team/sub/app/-/commit/" + Sha, "gitlab.example.com", "team/sub/app")]
    public async Task RepoAddresses_WhenCommitUrl_ShouldReturnRepoAndSha(string url, string host, string path)
    {
        var parts = await Addresses.ParseCommitUrlAsync(url);

        parts.Repo.Host.ShouldBe(host);
        parts.Repo.Path.ShouldBe(path);
        parts.Sha.ShouldBe(Sha);
    }

    [TestCase("a81d4e0")]
    [TestCase("https://github.com/acme/storefront/pull/12")]
    [TestCase("https://github.com/acme/storefront/commit/nothex")]
    [TestCase("https://gitlab.example.com/team/app/commit/" + Sha)]
    public async Task RepoAddresses_WhenNotACommitUrl_ShouldThrowValidationOnSha(string url)
    {
        var parse = async () => await Addresses.ParseCommitUrlAsync(url);

        var error = await parse.ShouldThrowAsync<ApiException>();
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("sha");
    }

    private sealed class Settings : IIntegrationSettings
    {
        public Task<IntegrationSettingsDto> GetAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(new IntegrationSettingsDto(null, "https://api.github.com", "https://gitlab.example.com", null, []));
    }
}
