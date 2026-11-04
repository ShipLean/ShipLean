using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Application.Features.Demos.Brand;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Demos;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Demos;

// K18's delivery over the real store, tokens and transfer tokens, and an in-memory blob store that can be told to throw. InspectAsync does the blob
// work with no transaction; PrepareAsync is called inside a transaction the test opens and only issues the logo's link. SHIP is the project, Joel the user.
[TestFixture]
public class DemoBrandSourceTests : ApiFixture
{
    private const string BaseUrl = "https://ship.example";
    private const string LogoKey = "demo-logos/b3/logo";
    private const string FallbackSentence = "The project's logo couldn't be prepared, so ShipLean's is used.";
    private static readonly Guid Project = SeedIds.Projects.Ship;
    private readonly SwitchableBlobStore _blobs = new(new InMemoryBlobStore());

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.RemoveAll<IBlobStore>();
        services.AddSingleton<IBlobStore>(_blobs);
    };

    [SetUp]
    public async Task ResetAsync()
    {
        _blobs.Reset();
        await SetRowAsync(null, string.Empty);
    }

    private async Task SetRowAsync(string? logoBlob, string tokens)
    {
        await using var database = await OpenDatabaseAsync();
        await database.ExecuteAsync(
            """
            INSERT INTO project_demo_settings (project_id, logo_blob, logo_version, tokens) VALUES (@Project, @logoBlob, 1, @tokens)
            ON CONFLICT (project_id) DO UPDATE SET logo_blob = @logoBlob, tokens = @tokens
            """,
            new { Project, logoBlob, tokens });
    }

    private async Task PutLogoAsync(string key = LogoKey, string contentType = "image/png", int length = 64)
    {
        using var content = new MemoryStream(new byte[length]);
        await _blobs.Inner.PutAsync(key, content, contentType, CancellationToken.None);
    }

    private async Task<Guid> NewItemAsync()
    {
        await using var database = await OpenDatabaseAsync();
        return await new WorkItemBuilder(Project).WithType("bau").WithTitle("Sprint demo").InsertAsync(database);
    }

    private IDemoBrandSource NewSource(out IServiceScope scope)
    {
        scope = Host.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<IDemoBrandSource>();
    }

    // Inspect, then Prepare inside a transaction that commits (or is rolled back). Returns the brand and the item the link was issued for.
    private async Task<DemoBrand> RunAsync(Guid itemId, bool commit = true)
    {
        var source = NewSource(out var scope);
        using (scope)
        {
            var inspection = await source.InspectAsync(Project);
            await using var connection = await Host.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();
            var brand = await source.PrepareAsync(inspection, Project, itemId, SeedIds.Users.Joel, null, "Test Agent (unverified)", BaseUrl, transaction);
            if (commit)
            {
                await transaction.CommitAsync();
            }
            else
            {
                await transaction.RollbackAsync();
            }

            return brand;
        }
    }

    private async Task<int> TransfersAsync(Guid itemId)
    {
        await using var database = await OpenDatabaseAsync();
        return await database.ExecuteScalarAsync<int>("SELECT count(*) FROM transfer_token WHERE work_item_id = @itemId", new { itemId });
    }

    private static string TokenOf(DemoBrand brand)
    {
        var url = brand.LogoUrl!;
        return Uri.UnescapeDataString(url[(url.IndexOf("token=", StringComparison.Ordinal) + "token=".Length)..]);
    }

    private async Task<TransferGrant?> FindAsync(string token)
    {
        using var scope = Host.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<ITransferTokens>().FindAsync(TransferKind.DemoLogo, token);
    }

    [Test]
    public async Task Source_WhenUnset_ShouldGiveShipLeansLogoAndTokens()
    {
        var item = await NewItemAsync();

        var brand = await RunAsync(item);

        brand.ShouldBe(DemoBrandDefaults.Brand);
        (await TransfersAsync(item)).ShouldBe(0);
    }

    [Test]
    public async Task Source_WhenNoRowAtAll_ShouldGiveShipLeans()
    {
        var item = await NewItemAsync();
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE project_demo_settings SET logo_blob = NULL, tokens = '' WHERE project_id = @Project", new { Project });
        }

        (await RunAsync(item)).ShouldBe(DemoBrandDefaults.Brand);
    }

    [Test]
    public async Task Source_WhenSet_ShouldGiveALinkAndOverlayTheTokens()
    {
        var item = await NewItemAsync();
        await PutLogoAsync();
        await SetRowAsync(LogoKey, "--color-accent: #c2410c;");

        var brand = await RunAsync(item);

        brand.LogoIsDefault.ShouldBeFalse();
        brand.LogoSvg.ShouldBeNull();
        brand.LogoContentType.ShouldBe("image/png");
        brand.LogoFallbackReason.ShouldBeNull();
        brand.LogoUrl.ShouldStartWith($"{BaseUrl}/api/downloads/demo-logo?token=");
        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();
        brand.LogoUrlExpiresAt.ShouldNotBeNull().ShouldBeInRange(now.AddHours(23.9), now.AddHours(24.1));
        brand.TokensAreDefault.ShouldBeFalse();
        brand.TokensCss.ShouldStartWith(DemoBrandDefaults.TokensCss.TrimEnd());
        brand.TokensCss.ShouldEndWith(":root {\n  --color-accent: #c2410c;\n}\n");
        (await TransfersAsync(item)).ShouldBe(1);
        var grant = await FindAsync(TokenOf(brand));
        grant.ShouldNotBeNull();
        grant.WorkItemId.ShouldBe(item);
        using var served = await Api.ActingAs(TestUser.Anonymous).GetAsync(brand.LogoUrl.Replace(BaseUrl, string.Empty, StringComparison.Ordinal).TrimStart('/'));
        served.StatusCode.ShouldBe(System.Net.HttpStatusCode.OK);
        (await served.Content.ReadAsByteArrayAsync()).Length.ShouldBe(64);
    }

    [Test]
    public async Task Source_WhenSomeTokensSet_ShouldKeepShipLeansForTheRest()
    {
        var item = await NewItemAsync();
        await SetRowAsync(null, "--color-bg: #101010;\n--font-sans: Georgia, serif;");

        var brand = await RunAsync(item);

        brand.TokensAreDefault.ShouldBeFalse();
        brand.LogoIsDefault.ShouldBeTrue();
        brand.TokensCss.ShouldBe($"{DemoBrandDefaults.TokensCss.TrimEnd()}\n:root {{\n  --color-bg: #101010;\n  --font-sans: \"Georgia\", serif;\n}}\n");
        brand.TokensCss.ShouldContain("--color-accent: #2563c9", Case.Insensitive);
    }

    [Test]
    public async Task Source_WhenUnknownNames_ShouldNotDeliverThem()
    {
        var item = await NewItemAsync();
        await SetRowAsync(null, "--sentinel-zq: 7777px;\n--zq-gap-unused: 4px;");

        var onlyUnknown = await RunAsync(item);

        onlyUnknown.TokensAreDefault.ShouldBeTrue();
        onlyUnknown.TokensCss.ShouldBe(DemoBrandDefaults.TokensCss);
        onlyUnknown.TokensCss.ShouldNotContain("sentinel");
        onlyUnknown.TokensCss.ShouldNotContain("7777");

        await SetRowAsync(null, "--sentinel-zq: 7777px;\n--color-text: #222222;\n--zq-gap-unused: 4px;");

        var mixed = await RunAsync(item);

        mixed.TokensAreDefault.ShouldBeFalse();
        mixed.TokensCss.ShouldEndWith(":root {\n  --color-text: #222222;\n}\n");
        mixed.TokensCss.ShouldNotContain("sentinel");
        mixed.TokensCss.ShouldNotContain("7777");
        mixed.TokensCss.ShouldNotContain("--zq-gap-unused");
    }

    [Test]
    public async Task Source_WhenStoredTokensBreakTheRules_ShouldUseShipLeans()
    {
        var item = await NewItemAsync();
        await SetRowAsync(null, "--color-bg: url(https://evil.example/x);\n--color-text: #222222;");

        var brand = await RunAsync(item);

        brand.TokensAreDefault.ShouldBeTrue();
        brand.TokensCss.ShouldBe(DemoBrandDefaults.TokensCss);
    }

    [Test]
    public async Task Source_ShouldNeverPutTheLogoInline()
    {
        var item = await NewItemAsync();
        await PutLogoAsync(length: 1024 * 1024);
        await SetRowAsync(LogoKey, string.Empty);

        var brand = await RunAsync(item);

        brand.LogoSvg.ShouldBeNull();
        brand.LogoUrl.ShouldNotBeNull();
        var wire = JsonSerializer.Serialize(brand);
        wire.Length.ShouldBeLessThan(DemoBrandDefaults.TokensCss.Length + 2000);
        wire.ShouldNotContain("data:");
        wire.ShouldNotContain("AAAAAAAA");
    }

    [Test]
    public async Task Source_WhenTransactionRollsBack_ShouldLeaveNoLogoToken()
    {
        var item = await NewItemAsync();
        await PutLogoAsync();
        await SetRowAsync(LogoKey, string.Empty);

        var brand = await RunAsync(item, commit: false);

        brand.LogoUrl.ShouldNotBeNull();
        (await TransfersAsync(item)).ShouldBe(0);
        (await FindAsync(TokenOf(brand))).ShouldBeNull();
    }

    [Test]
    public async Task Source_WhenBlobStoreThrows_ShouldFallBackWithTheReason()
    {
        var item = await NewItemAsync();
        await PutLogoAsync();
        await SetRowAsync(LogoKey, "--color-accent: #c2410c;");
        _blobs.ThrowOnExists = true;

        var brand = await RunAsync(item);

        AssertFellBack(brand);
        brand.TokensAreDefault.ShouldBeFalse();
        (await TransfersAsync(item)).ShouldBe(0);
    }

    [Test]
    public async Task Source_WhenBlobStoreTimesOut_ShouldFallBackWithTheReason()
    {
        var item = await NewItemAsync();
        await PutLogoAsync();
        await SetRowAsync(LogoKey, "--color-accent: #c2410c;");
        _blobs.TimeOutOnExists = true;

        var brand = await RunAsync(item);

        AssertFellBack(brand);
        (await TransfersAsync(item)).ShouldBe(0);
    }

    [Test]
    public async Task Source_WhenBlobGone_ShouldFallBackWithTheReason()
    {
        var item = await NewItemAsync();
        await SetRowAsync("demo-logos/b3/never-stored", string.Empty);

        var brand = await RunAsync(item);

        AssertFellBack(brand);
        (await TransfersAsync(item)).ShouldBe(0);
    }

    [Test]
    public async Task Source_WhenTheBlobIsNotAnImageType_ShouldFallBackWithTheReason()
    {
        var item = await NewItemAsync();
        await PutLogoAsync(contentType: "text/html");
        await SetRowAsync(LogoKey, string.Empty);

        var brand = await RunAsync(item);

        brand.LogoFallbackReason.ShouldBe(FallbackSentence);
        AssertFellBack(brand);
    }

    [Test]
    public async Task Source_WhenFallingBack_ShouldHoldNoExceptionText()
    {
        var item = await NewItemAsync();
        await PutLogoAsync();
        await SetRowAsync(LogoKey, string.Empty);
        _blobs.ThrowOnExists = true;
        var source = NewSource(out var scope);
        using (scope)
        {
            var inspection = await source.InspectAsync(Project);
            var brand = await RunAsync(item);

            JsonSerializer.Serialize(inspection).ShouldNotContain(SwitchableBlobStore.SecretText);
            JsonSerializer.Serialize(brand).ShouldNotContain(SwitchableBlobStore.SecretText);
            inspection.LogoFallbackReason.ShouldBe(FallbackSentence);
            brand.LogoFallbackReason.ShouldBe(FallbackSentence);
        }
    }

    [Test]
    public async Task Inspect_ShouldGiveTheReasonAndNoBlobWhenTheStoreThrows()
    {
        await PutLogoAsync();
        await SetRowAsync(LogoKey, string.Empty);
        _blobs.ThrowOnExists = true;
        var source = NewSource(out var scope);
        using (scope)
        {
            var inspection = await source.InspectAsync(Project);

            inspection.LogoBlob.ShouldBeNull();
            inspection.LogoContentType.ShouldBeNull();
            inspection.LogoFallbackReason.ShouldBe(FallbackSentence);
        }
    }

    [Test]
    public async Task Prepare_ShouldMakeNoBlobStoreCall()
    {
        var item = await NewItemAsync();
        await PutLogoAsync();
        await SetRowAsync(LogoKey, string.Empty);
        var source = NewSource(out var scope);
        using (scope)
        {
            var inspection = await source.InspectAsync(Project);
            var afterInspect = _blobs.Calls;
            afterInspect.ShouldBeGreaterThan(0);
            await using var connection = await Host.Services.GetRequiredService<NpgsqlDataSource>().OpenConnectionAsync();
            await using var transaction = await connection.BeginTransactionAsync();

            var brand = await source.PrepareAsync(inspection, Project, item, SeedIds.Users.Joel, null, "Test Agent (unverified)", BaseUrl, transaction);

            brand.LogoUrl.ShouldNotBeNull();
            _blobs.Calls.ShouldBe(afterInspect);
        }
    }

    private static void AssertFellBack(DemoBrand brand)
    {
        brand.LogoIsDefault.ShouldBeTrue();
        brand.LogoSvg.ShouldBe(DemoBrandDefaults.LogoSvg);
        brand.LogoUrl.ShouldBeNull();
        brand.LogoContentType.ShouldBeNull();
        brand.LogoUrlExpiresAt.ShouldBeNull();
        brand.LogoFallbackReason.ShouldBe(FallbackSentence);
    }

    // The in-memory blob store, counting every call and throwing from ExistsAsync when told to, with text that must never reach a brand.
    private sealed class SwitchableBlobStore(InMemoryBlobStore inner) : IBlobStore
    {
        public const string SecretText = "secret-blob-host.internal:9000";

        private int _calls;

        public InMemoryBlobStore Inner => inner;

        public bool ThrowOnExists { get; set; }

        public bool TimeOutOnExists { get; set; }

        public int Calls => Volatile.Read(ref _calls);

        public void Reset() => (ThrowOnExists, TimeOutOnExists) = (false, false);

        public Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return inner.PutAsync(key, content, contentType, cancellationToken);
        }

        public Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return inner.GetAsync(key, cancellationToken);
        }

        public Task<Stream?> OpenAtAsync(string key, long offset, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            return inner.OpenAtAsync(key, offset, cancellationToken);
        }

        public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            if (TimeOutOnExists)
            {
                throw new TaskCanceledException($"The request to {SecretText} was canceled due to the configured HttpClient.Timeout of 100 seconds elapsing.");
            }
            return ThrowOnExists ? throw new IOException($"Connection refused to {SecretText}") : inner.ExistsAsync(key, cancellationToken);
        }
    }
}
