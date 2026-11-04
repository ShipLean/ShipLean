using ShipLean.LocalEnvironment.Database;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// Half of the YouTube environment pair fails host start, and the failure names the setting that is missing.
[TestFixture]
public class YouTubeClientStartupTests
{
    private ClonedDatabase _database = null!;

    [OneTimeSetUp]
    public async Task CloneAsync() => _database = await ShipLeanDatabase.CloneSeedAsync();

    [OneTimeTearDown]
    public async Task DropAsync() => await _database.DisposeAsync();

    [Test]
    public void Start_WhenEnvHasOneOfTwo_ShouldFailNamingTheSetting()
    {
        var withoutSecret = StartFailure(new() { ["SHIPLEAN_YOUTUBE_CLIENT_ID"] = "only-id" });
        var withoutId = StartFailure(new() { ["SHIPLEAN_YOUTUBE_CLIENT_SECRET"] = "only-secret-9999" });

        withoutSecret.ShouldContain("SHIPLEAN_YOUTUBE_CLIENT_SECRET is missing");
        withoutId.ShouldContain("SHIPLEAN_YOUTUBE_CLIENT_ID is missing");
        withoutId.ShouldNotContain("only-secret-9999");
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
