using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Links.GitLab;
using ShipLean.Application.Http;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.TaskChanges;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Http;
using ShipLean.WebApi.Tests.Links.GitHub;
using Shouldly;
using FixedHostResolver = ShipLean.WebApi.Tests.Links.OpenGraph.FixedHostResolver;

namespace ShipLean.WebApi.Tests.TaskChanges.Hosts;

// A self-hosted GitLab on a private network: the configured GitLabUrl resolves to a private address, and the app's own GitLab
// clients still reach it. The real PublicAddressCheck stays registered; "gitlab.internal.test" resolves to the stub on loopback.
[TestFixture]
public class GitLabPrivateAddressTests : EmptyApiFixture
{
    private const string GitLabHost = "gitlab.internal.test";
    private const string Sha = "c0ffee00c0ffee00c0ffee00c0ffee00c0ffee00";
    private const string Project = "/api/v4/projects/acme%2Fstorefront";

    private StubHttpServer _server = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IHostResolver>(new FixedHostResolver(new Dictionary<string, string> { [GitLabHost] = "127.0.0.1" }));
        services.AddSingleton<IIntegrationSettings>(new FixedIntegrationSettings(
            new IntegrationSettingsDto(null, "https://api.github.invalid", $"http://{GitLabHost}:{_server.BaseAddress.Port}", null, [])));
    };

    protected override async Task<ClonedDatabase> CloneDatabaseAsync()
    {
        _server = await StubHttpServer.StartAsync();
        _server.Respond($"{Project}/repository/commits/{Sha}", $$"""
            { "id": "{{Sha}}", "parent_ids": [], "title": "Fix totals", "author_name": "Tom", "authored_date": "2026-11-12T08:30:00.000+00:00",
              "stats": { "additions": 1, "deletions": 1, "total": 2 } }
            """, "application/json");
        _server.Respond($"{Project}/repository/commits/{Sha}/diff", """
            [ { "old_path": "a.ts", "new_path": "a.ts", "new_file": false, "renamed_file": false, "deleted_file": false, "diff": "@@ -1 +1 @@\n-a\n+b\n" } ]
            """, "application/json");
        _server.Respond($"{Project}/merge_requests/31", """{ "iid": 31, "title": "Rotate keys", "state": "opened", "draft": false, "author": { "username": "mira" } }""", "application/json");
        return await base.CloneDatabaseAsync();
    }

    [OneTimeTearDown]
    public async Task StopGitLab() => await _server.DisposeAsync();

    [Test]
    public async Task ReadCommit_WhenGitLabUrlResolvesToAPrivateAddress_ShouldReadIt()
    {
        using var scope = Host.Services.CreateScope();

        var read = await scope.ServiceProvider.GetRequiredService<ICodeHost>()
            .ReadCommitAsync(new RepoRef(CodeProvider.Gitlab, GitLabHost, "acme/storefront"), Sha);

        read.Status.ShouldBe(CommitReadStatus.Found);
        read.Meta!.Subject.ShouldBe("Fix totals");
    }

    [Test]
    public async Task LinkCard_WhenGitLabUrlResolvesToAPrivateAddress_ShouldReadTheMergeRequest()
    {
        using var scope = Host.Services.CreateScope();
        var source = scope.ServiceProvider.GetServices<ILinkCardSource>().OfType<GitLabSource>().Single();

        var fetch = await source.FetchAsync(new Uri($"http://{GitLabHost}:{_server.BaseAddress.Port}/acme/storefront/-/merge_requests/31"), CancellationToken.None);

        fetch.State.ShouldBe(LinkState.Open);
    }
}
