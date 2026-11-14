using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using ShipLean.Application.Features.TaskChanges.Hosts;
using ShipLean.Contracts.TaskChanges;
using ShipLean.LocalEnvironment.Database;
using ShipLean.WebApi.Features.TaskChanges;
using ShipLean.WebApi.Realtime;
using Shouldly;

namespace ShipLean.WebApi.Tests.TaskChanges.Hosts;

[TestFixture]
public class CodePatchCacheTests
{
    private static readonly RepoRef Repo = new(CodeProvider.Github, "github.com", "acme/storefront");

    private FakeTimeProvider _time = null!;
    private CountingHost _inner = null!;
    private CachingCodeHost _host = null!;

    [SetUp]
    public void BuildTheCache()
    {
        _time = new FakeTimeProvider();
        _inner = new CountingHost();
        _host = new CachingCodeHost(_inner, new MemoryCodePatchCache(_time, 100));
    }

    [Test]
    public async Task CodePatchCache_WhenReadTwiceWithinADay_ShouldCallHostOnce()
    {
        var first = await _host.ReadPatchAsync(Repo, "abc");
        _time.Advance(TimeSpan.FromHours(23));
        var second = await _host.ReadPatchAsync(Repo, "ABC");

        _inner.Patches.ShouldBe(1);
        second.UnifiedDiff.ShouldBe(first.UnifiedDiff);
    }

    [Test]
    public async Task CodePatchCache_WhenADayHasPassed_ShouldReadAgain()
    {
        await _host.ReadPatchAsync(Repo, "abc");
        _time.Advance(TimeSpan.FromHours(24) + TimeSpan.FromSeconds(1));
        await _host.ReadPatchAsync(Repo, "abc");

        _inner.Patches.ShouldBe(2);
    }

    [Test]
    public async Task CodePatchCache_WhenMissing_ShouldKeepItForFiveMinutesOnly()
    {
        _inner.Status = CommitReadStatus.Missing;
        await _host.ReadPatchAsync(Repo, "abc");
        _time.Advance(TimeSpan.FromMinutes(4));
        await _host.ReadPatchAsync(Repo, "abc");
        _inner.Patches.ShouldBe(1);

        _time.Advance(TimeSpan.FromMinutes(2));
        await _host.ReadPatchAsync(Repo, "abc");
        _inner.Patches.ShouldBe(2);
    }

    [Test]
    public async Task CodePatchCache_WhenUnreadable_ShouldNotKeepIt()
    {
        _inner.Status = CommitReadStatus.Unreadable;
        await _host.ReadPatchAsync(Repo, "abc");
        await _host.ReadPatchAsync(Repo, "abc");

        _inner.Patches.ShouldBe(2);
    }

    [Test]
    public async Task CodePatchCache_WhenRangesDiffer_ShouldKeepThemApart()
    {
        await _host.ReadRangePatchAsync(Repo, "a", "b");
        await _host.ReadRangePatchAsync(Repo, "a", "c");
        await _host.ReadRangePatchAsync(Repo, "a", "b");

        _inner.Ranges.ShouldBe(2);
    }

    [Test]
    public async Task CodePatchCache_WhenFull_ShouldStayWithinItsMaximum()
    {
        var host = new CachingCodeHost(_inner, new MemoryCodePatchCache(_time, 2));
        foreach (var sha in new[] { "a", "b", "c" })
        {
            await host.ReadPatchAsync(Repo, sha);
        }

        await host.ReadPatchAsync(Repo, "c");
        await host.ReadPatchAsync(Repo, "a");

        _inner.Patches.ShouldBe(4);
    }

    [Test]
    public async Task CodePatchCache_WhenRedisConfigured_ShouldShareBetweenInstances()
    {
        var redis = await RedisServer.ScenarioConnectionStringAsync();
        await using var a = InstanceOn(redis);
        await using var b = InstanceOn(redis);
        var key = $"{Guid.NewGuid():N}:abc";

        await a.GetRequiredService<ICodePatchCache>().SetAsync(key, new PatchRead(CommitReadStatus.Found, "diff"), TimeSpan.FromMinutes(1), CancellationToken.None);

        (await b.GetRequiredService<ICodePatchCache>().GetAsync(key, CancellationToken.None))!.UnifiedDiff.ShouldBe("diff");
    }

    [Test]
    public void CodePatchCache_WhenRedisNotConfigured_ShouldBeMemory()
    {
        using var provider = InstanceOn(null);

        provider.GetRequiredService<ICodePatchCache>().ShouldBeOfType<MemoryCodePatchCache>();
    }

    private static ServiceProvider InstanceOn(string? redis)
    {
        var settings = new Dictionary<string, string?>();
        if (redis is not null)
        {
            settings[RealtimeServices.RedisConnectionKey] = redis;
        }

        var configuration = new ConfigurationBuilder().AddInMemoryCollection(settings).Build();
        var services = new ServiceCollection();
        services.AddSingleton<TimeProvider>(new FakeTimeProvider());
        new CodePatchCacheServices().AddServices(services, configuration);
        return (ServiceProvider)new DefaultServiceProviderFactory().CreateServiceProvider(services);
    }

    private sealed class CountingHost : ICodeHost
    {
        public int Patches { get; private set; }

        public int Ranges { get; private set; }

        public CommitReadStatus Status { get; set; } = CommitReadStatus.Found;

        public Task<CommitRead> ReadCommitAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
            => Task.FromResult(new CommitRead(Status, null, null));

        public Task<PatchRead> ReadPatchAsync(RepoRef repo, string sha, CancellationToken cancellationToken = default)
        {
            Patches++;
            return Task.FromResult(new PatchRead(Status, Status == CommitReadStatus.Found ? $"diff {Patches}" : null));
        }

        public Task<PatchRead> ReadRangePatchAsync(RepoRef repo, string baseSha, string headSha, CancellationToken cancellationToken = default)
        {
            Ranges++;
            return Task.FromResult(new PatchRead(Status, "range"));
        }
    }
}
