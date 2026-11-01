using System.Net.Http.Json;
using ShipLean.Contracts;
using ShipLean.Contracts.Auth;
using ShipLean.Contracts.Projects;
using ShipLean.LocalEnvironment;
using ShipLean.LocalEnvironment.Blobs;
using ShipLean.LocalEnvironment.Database;
using ShipLean.LocalHost;

namespace ShipLean.WebApi.Tests.LocalHost;

// The real host on a free port against its own database, with the bundle path pointed at a folder the test controls. Not parallel:
// the Local blob store is the WebApi project's shared data/blobs folder, which the API fixtures fill too.
[TestFixture]
[NonParallelizable]
public class LocalHostRunTests
{
    // Seeded by db/seed/0010_attachments.sql.
    private const string SeededAttachmentId = "00000005-0000-4000-8003-000000000901";

    private readonly string _database = "shiplean_lh_" + Guid.NewGuid().ToString("N")[..8];
    private string _bundleFolder = "";

    [SetUp]
    public void CreateBundleFolder() => _bundleFolder = Directory.CreateTempSubdirectory("shiplean-lh-").FullName;

    [TearDown]
    public async Task Clean()
    {
        Directory.Delete(_bundleFolder, recursive: true);
        await ShipLeanDatabase.DropNamedAsync(_database);
    }

    [Test]
    public async Task Start_WhenReady_ShouldPrintTheFourReadyLines()
    {
        var output = new StringWriter();
        await using var host = await StartAsync(output, withBundle: true);

        Lines(output).ShouldBe(
        [
            $"ShipLean is running at {host.BaseUrl}",
            $"Sign in as {LocalSignIn.Email} / {LocalSignIn.Password}",
            $"SHIPLEAN_LOCAL_URL={host.BaseUrl}",
            "Press Ctrl+C to stop.",
        ]);
    }

    [Test]
    public async Task Start_WhenBundleIsMissing_ShouldWarnFirst()
    {
        var output = new StringWriter();
        await using var host = await StartAsync(output, withBundle: false);

        var lines = Lines(output);
        lines.Length.ShouldBe(5);
        lines[0].ShouldStartWith("No client bundle in src/ShipLean.WebApi/wwwroot:");
        lines[0].ShouldContain("pnpm run build");
        lines[0].ShouldContain("pnpm run dev");
        lines[0].ShouldContain(":3000");
        lines[1].ShouldBe($"ShipLean is running at {host.BaseUrl}");
    }

    [Test]
    public async Task Start_WhenReady_ShouldServeHealth()
    {
        await using var host = await StartAsync(new StringWriter(), withBundle: true);
        using var client = new HttpClient { BaseAddress = new Uri(host.BaseUrl) };

        using var response = await client.GetAsync("/health");

        response.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Test]
    public async Task Start_WhenSignedInWithLocalSignIn_ShouldSucceed()
    {
        await using var host = await StartAsync(new StringWriter(), withBundle: true);
        using var client = NewClient(host);

        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(LocalSignIn.Email, LocalSignIn.Password));

        response.IsSuccessStatusCode.ShouldBeTrue(await response.Content.ReadAsStringAsync());
    }

    [Test]
    public async Task Start_WhenReady_ShouldServeASeededAttachment()
    {
        // Earlier runs and the API fixtures leave the seed blobs in the shared folder; only the host's own put may supply them.
        var seedFolder = Path.Combine(RepoLayout.WebApiProject, "data", "blobs", "seed");
        if (Directory.Exists(seedFolder))
        {
            Directory.Delete(seedFolder, recursive: true);
        }
        await using var host = await StartAsync(new StringWriter(), withBundle: true);
        using var client = await SignedInAsync(host);

        var text = await client.GetStringAsync($"/api/attachments/{SeededAttachmentId}/content");

        text.ShouldBe(SeedAttachmentBlobs.Contents["seed/attachments/wallet-scope.txt"]);
    }

    [Test]
    public async Task Start_WhenDataIsKeep_ShouldKeepAProjectCreatedEarlier()
    {
        await using (var first = await StartAsync(new StringWriter(), withBundle: true))
        {
            using var client = await SignedInAsync(first);
            using var created = await client.PostAsJsonAsync("/api/projects", new ProjectCreate("Kept across runs", "KEPT", null, [], [1, 2, 3, 4, 5], null));
            created.IsSuccessStatusCode.ShouldBeTrue(await created.Content.ReadAsStringAsync());
        }

        await using var second = await StartAsync(new StringWriter(), withBundle: true, LocalHostData.Keep);
        using var again = await SignedInAsync(second);

        (await again.GetStringAsync("/api/projects")).ShouldContain("KEPT");
    }

    [Test]
    public async Task Start_WhenDisposed_ShouldStopListeningAndCancelStopping()
    {
        var host = await StartAsync(new StringWriter(), withBundle: true);
        var stopping = host.Stopping;
        using var client = new HttpClient { BaseAddress = new Uri(host.BaseUrl) };
        (await client.GetAsync("/health")).IsSuccessStatusCode.ShouldBeTrue();

        await host.DisposeAsync();

        stopping.IsCancellationRequested.ShouldBeTrue();
        await Should.ThrowAsync<HttpRequestException>(() => client.GetAsync("/health"));
    }

    private async Task<LocalHostInstance> StartAsync(TextWriter output, bool withBundle, LocalHostData data = LocalHostData.Fresh)
    {
        var bundle = Path.Combine(_bundleFolder, "index.html");
        if (withBundle)
        {
            await File.WriteAllTextAsync(bundle, "<html></html>");
        }
        return await LocalHostRunner.StartAsync(new LocalHostOptions { Port = 0, Data = data, Database = _database }, bundle, output);
    }

    private static async Task<HttpClient> SignedInAsync(LocalHostInstance host)
    {
        var client = NewClient(host);
        using var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(LocalSignIn.Email, LocalSignIn.Password), CancellationToken.None);
        response.EnsureSuccessStatusCode();
        return client;
    }

    // The cookie jar of a browser, and the header the SPA sends with every write.
    private static HttpClient NewClient(LocalHostInstance host)
    {
        var client = new HttpClient { BaseAddress = new Uri(host.BaseUrl) };
        client.DefaultRequestHeaders.Add(ClientHeader.Name, "local-host-tests");
        return client;
    }

    private static string[] Lines(StringWriter output) => output.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
}
