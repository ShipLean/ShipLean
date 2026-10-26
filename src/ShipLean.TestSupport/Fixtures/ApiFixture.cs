using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using ShipLean.TestSupport.Database;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.TestSupport.Fixtures;

// Base class for API tests. A test class is one scenario on one database: the fixture clones the seed template,
// starts its own Kestrel instance of the app against the clone, and tears both down at the end. Tests inside run in
// [Order]; independent scenarios are separate classes, and classes run in parallel. Derive from EmptyApiFixture to
// start without the seed rows.
public abstract class ApiFixture
{
    private ClonedDatabase _database = null!;
    private ShipLeanHost _host = null!;

    protected HttpClient Client { get; private set; } = null!;

    // The same client wrapped to send ClientHeader and JSON and to read ApiError bodies.
    protected ApiClient Api { get; private set; } = null!;

    protected ShipLeanHost Host => _host;

    protected string ConnectionString => _database.ConnectionString;

    // Doubles go here; they are applied through the host's ConfigureTestServices.
    protected virtual Action<IServiceCollection>? ConfigureTestServices => null;

    // Who requests without a sign-in cookie act as (TestAuthentication): no login round trip. Override for another
    // seeded user or TestUser.Anonymous; a request with the real cookie is authenticated by the cookie.
    protected virtual TestUser SignedInAs => TestUser.Joel;

    // False starts the host with no test sign-in (user null): only the real schemes sign anyone in, and SignedInAs is
    // not installed. TestUser.Anonymous is not the same: it still installs TestAuthentication.
    protected virtual bool UseTestSignIn => true;

    // True for the rare test that must reach the app from a non-loopback address.
    protected virtual bool ListenOnAllInterfaces => false;

    // More app configuration for the host, such as a SHIPLEAN_* variable the scenario turns on.
    protected virtual IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>();

    protected virtual Task<ClonedDatabase> CloneDatabaseAsync() => ShipLeanDatabase.CloneSeedAsync();

    protected Task<NpgsqlConnection> OpenDatabaseAsync() => _database.OpenAsync();

    // Runs after the database is cloned and before HostSettings is read and the host starts: for something the host's
    // settings name, such as an identity provider's URL. NUnit runs a base [OneTimeSetUp] before a derived one, so a
    // subclass can't start it in its own.
    protected virtual Task BeforeHostStartAsync() => Task.CompletedTask;

    // Runs at teardown after the host and the database are disposed: for something started in BeforeHostStartAsync
    // that the host uses until it stops. NUnit runs a derived [OneTimeTearDown] before the base one, so a subclass's own
    // teardown would run while the host is still up.
    protected virtual Task AfterHostStopAsync() => Task.CompletedTask;

    [OneTimeSetUp]
    public async Task StartScenarioAsync()
    {
        _database = await CloneDatabaseAsync();
        await BeforeHostStartAsync();
        _host = ShipLeanHost.Start(_database.ConnectionString, UseTestSignIn ? SignedInAs : null, ConfigureTestServices, ListenOnAllInterfaces, settings: HostSettings);
        Client = _host.CreateClient();
        Api = new ApiClient(Client);
    }

    // A service called straight from Host.Services (no request) acts as SignedInAs, as a request would.
    [SetUp]
    public void ActAsSignedInUser() => _host.ActAs(SignedInAs.Id);

    [OneTimeTearDown]
    public async Task EndScenarioAsync()
    {
        Client?.Dispose();
        if (_host != null)
        {
            await _host.DisposeAsync();
        }
        if (_database != null)
        {
            await _database.DisposeAsync();
        }
        await AfterHostStopAsync();
    }
}
