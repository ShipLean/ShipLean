using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;
using NUnit.Framework.Interfaces;
using ShipLean.Contracts;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.TestSupport.Fixtures;

// Base class for browser tests. Same shape as ApiFixture (one cloned database and one Kestrel app per test class)
// plus a browser context per class from the run's shared Chromium and a fresh page per test. A trace is recorded per
// test and kept under artifacts/playwright-traces/ only when the test fails.
// The tests drive the shipped bundle, so build the client first:
//   cd src/ShipLean.Client && pnpm install && pnpm run build     (emits into src/ShipLean.WebApi/wwwroot)
public abstract partial class AcceptanceFixture
{
    private ClonedDatabase _database = null!;
    private ShipLeanHost _host = null!;
    private IBrowserContext _context = null!;
    private string? _signedInAs;

    protected IPage Page { get; private set; } = null!;

    protected string DatabaseName { get; private set; } = null!;

    protected ShipLeanHost Host => _host;

    protected string BaseUrl => _host.BaseUrl;

    protected string ConnectionString => _database.ConnectionString;

    protected virtual Action<IServiceCollection>? ConfigureTestServices => null;

    // More app configuration for the host, such as a SHIPLEAN_* variable the scenario turns on.
    protected virtual IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>();

    protected abstract Task<ClonedDatabase> CloneDatabaseAsync();

    // Runs after the database is cloned and before HostSettings is read and the host starts: for something the host's
    // settings name, such as an identity provider's URL.
    protected virtual Task BeforeHostStartAsync() => Task.CompletedTask;

    protected static ILocatorAssertions Expect(ILocator locator) => Assertions.Expect(locator);

    protected static IPageAssertions Expect(IPage page) => Assertions.Expect(page);

    // Who the page is signed in as before each test: Joel, through the real sign-in (POST /api/auth/login on the page's
    // browser context, so the cookie is the real one). Override with another seeded user or TestUser.Anonymous.
    protected virtual TestUser SignedInAs => TestUser.Joel;

    // Signs the page's context in as SignedInAs unless it already is: the cookie outlives a test, so a class signs in
    // once per user (the login endpoint is rate limited).
    protected virtual async Task SignInAsync()
    {
        if (SignedInAs is { Email: { } email, Password: { } password } && _signedInAs != email)
        {
            await SignInAsync(email, password);
        }
    }

    // Signs in through the login endpoint; for a user the test arranged itself (SignedInAs only knows seeded ones).
    protected async Task SignInAsync(string email, string password)
    {
        var response = await _context.APIRequest.PostAsync("/api/auth/login", new APIRequestContextOptions
        {
            DataObject = new { email, password },
            Headers = new Dictionary<string, string> { [ClientHeader.Name] = "acceptance-test" },
        });
        Assert.That(response.Ok, $"Signing in as {email} answered {response.Status}: {await response.TextAsync()}");
        _signedInAs = email;
    }

    // Ends the page's session; the next SignInAsync signs in again.
    protected async Task SignOutAsync()
    {
        await _context.APIRequest.PostAsync("/api/auth/logout", new APIRequestContextOptions
        {
            Headers = new Dictionary<string, string> { [ClientHeader.Name] = "acceptance-test" },
        });
        _signedInAs = null;
    }

    [OneTimeSetUp]
    public async Task StartScenarioAsync()
    {
        Assert.That(
            ShipLeanHost.HasClientBundle(),
            "The SPA bundle is missing. Build the client first: cd src/ShipLean.Client && pnpm install && pnpm run build "
            + "(it emits into src/ShipLean.WebApi/wwwroot).");
        _database = await CloneDatabaseAsync();
        DatabaseName = _database.Name;
        await BeforeHostStartAsync();
        _host = ShipLeanHost.Start(_database.ConnectionString, null, ConfigureTestServices, settings: HostSettings);
        var browser = await SharedBrowser.GetAsync();
        _context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            BaseURL = _host.BaseUrl,
            ViewportSize = new ViewportSize { Width = 1280, Height = 800 },
            TimezoneId = "UTC",
        });
        await _context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true });
    }

    [SetUp]
    public async Task StartTestAsync()
    {
        await _context.Tracing.StartChunkAsync();
        Page = await _context.NewPageAsync();
        await SignInAsync();
    }

    [TearDown]
    public async Task EndTestAsync()
    {
        var failed = TestContext.CurrentContext.Result.Outcome.Status == TestStatus.Failed;
        string? tracePath = null;
        if (failed)
        {
            var name = UnsafeFileChars().Replace(TestContext.CurrentContext.Test.FullName, "_");
            Directory.CreateDirectory(Path.Combine(RepoPaths.ArtifactsDirectory, "playwright-traces"));
            tracePath = Path.Combine(RepoPaths.ArtifactsDirectory, "playwright-traces", $"{name}.zip");
        }
        if (_context != null)
        {
            await _context.Tracing.StopChunkAsync(new TracingStopChunkOptions { Path = tracePath });
        }
        if (Page != null)
        {
            await Page.CloseAsync();
        }
    }

    [OneTimeTearDown]
    public async Task EndScenarioAsync()
    {
        if (_context != null)
        {
            await _context.CloseAsync();
        }
        if (_host != null)
        {
            await _host.DisposeAsync();
        }
        if (_database != null)
        {
            await _database.DisposeAsync();
        }
    }

    [GeneratedRegex(@"[^A-Za-z0-9._-]")]
    private static partial Regex UnsafeFileChars();
}
