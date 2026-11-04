using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.AspNetCore.Http;
using ShipLean.Application.Features.Demos.Brand;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Demos;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Demos;

// K18's two logo routes with explicit fakes: the member preview is version-keyed like a profile photo, the agent's download never is.
[TestFixture]
public class DemoLogoCacheTests : ApiFixture
{
    private static readonly string LogoPath = $"/api/projects/{SeedIds.Projects.Ship}/demo-brand/logo";

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IDemoBrand>();
        services.AddScoped<IDemoBrand, VersionThreeBrand>();
        services.RemoveAll<IDemoLogoDownload>();
        services.AddScoped<IDemoLogoDownload, FixedLogoDownload>();
    };

    [Test]
    public async Task MemberLogo_WhenVersionIsCurrent_ShouldBeCachedImmutably()
    {
        using var served = await Api.ActingAs(TestUser.Joel).GetAsync($"{LogoPath}?v=3");

        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        served.Content.Headers.GetValues("Content-Disposition").ShouldBe(["inline; filename=\"logo\""]);
        served.Headers.CacheControl!.ToString().ShouldBe("max-age=31536000, private, immutable");
    }

    [TestCase("?v=2")]
    [TestCase("?v=abc")]
    [TestCase("")]
    public async Task MemberLogo_WhenVersionIsNotCurrent_ShouldRevalidate(string query)
    {
        using var served = await Api.ActingAs(TestUser.Joel).GetAsync($"{LogoPath}{query}");

        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Headers.CacheControl!.ToString().ShouldBe("no-cache, private");
    }

    [Test]
    public async Task AgentDownload_WhenTokenIsLive_ShouldNeverBeCachedImmutably()
    {
        using var served = await Api.GetAsync("/api/downloads/demo-logo?token=live&v=3");

        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Content.Headers.GetValues("Content-Disposition").ShouldBe(["attachment; filename=\"logo\""]);
        served.Headers.CacheControl!.ToString().ShouldBe("no-cache, private");
    }

    private static BlobContent Logo() => new(new MemoryStream("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"u8.ToArray()), "image/svg+xml");

    private sealed class VersionThreeBrand : IDemoBrand
    {
        public Task<ProjectDemoBrandDto> ReadAsync(Guid projectId, CancellationToken cancellationToken = default)
            => Task.FromResult(new ProjectDemoBrandDto($"/api/projects/{projectId}/demo-brand/logo?v=3", "image/svg+xml", string.Empty, []));

        public Task<BlobContent> OpenLogoAsync(Guid projectId, CancellationToken cancellationToken = default) => Task.FromResult(Logo());

        public Task<ProjectDemoBrandDto> SaveTokensAsync(Guid projectId, string? tokens, CancellationToken cancellationToken = default) => ReadAsync(projectId, cancellationToken);

        public Task<ProjectDemoBrandDto> RemoveLogoAsync(Guid projectId, CancellationToken cancellationToken = default) => ReadAsync(projectId, cancellationToken);
    }

    private sealed class FixedLogoDownload : IDemoLogoDownload
    {
        public Task<BlobContent> OpenAsync(string token, HttpRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Logo());
    }
}
