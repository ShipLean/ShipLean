using ShipLean.Application.Identity;
using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Auth.Google;

// A bad SHIPLEAN_GOOGLE_* setting fails host start, and the failure names the setting.
[TestFixture]
public class GoogleSettingsStartupTests
{
    private ClonedDatabase _database = null!;

    [OneTimeSetUp]
    public async Task CloneAsync() => _database = await ShipLeanDatabase.CloneSeedAsync();

    [OneTimeTearDown]
    public async Task DropAsync() => await _database.DisposeAsync();

    [Test]
    public void Startup_WhenClientSecretIsMissing_ShouldFailNamingIt()
    {
        var failure = StartFailure(new() { [GoogleSignInSettings.ClientIdKey] = "id" });

        failure.ShouldContain(GoogleSignInSettings.ClientSecretKey);
    }

    [Test]
    public void Startup_WhenClientIdIsMissing_ShouldFailNamingIt()
    {
        var failure = StartFailure(new() { [GoogleSignInSettings.ClientSecretKey] = "shh" });

        failure.ShouldContain(GoogleSignInSettings.ClientIdKey);
    }

    [Test]
    public void Startup_WhenProxySignInIsAlsoOn_ShouldFailNamingBoth()
    {
        var failure = StartFailure(new()
        {
            [GoogleSignInSettings.ClientIdKey] = "id",
            [GoogleSignInSettings.ClientSecretKey] = "shh",
            [ProxySignInSettings.EmailHeaderKey] = "Remote-Email",
        });

        failure.ShouldContain(GoogleSignInSettings.ClientIdKey);
        failure.ShouldContain(ProxySignInSettings.EmailHeaderKey);
    }

    private string StartFailure(Dictionary<string, string?> settings)
    {
        var exception = Should.Throw<Exception>(() => ShipLeanHost.Start(_database.ConnectionString, user: null, settings: settings));
        var messages = new List<string>();
        for (Exception? e = exception; e is not null; e = e.InnerException)
        {
            messages.Add(e.Message);
        }
        return string.Join(" | ", messages);
    }
}
