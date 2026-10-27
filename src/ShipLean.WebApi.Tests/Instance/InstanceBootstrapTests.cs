using System.Net;
using System.Net.Http.Json;
using Dapper;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Instance;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Auth;

namespace ShipLean.WebApi.Tests.Instance;

// Schema and migrations only: real startup must prepare the instance before the first admin signs in.
[TestFixture]
public class InstanceBootstrapTests : EmptyApiFixture
{
    private const string Email = "first.admin@example.test";
    private const string Password = "first-admin-password";
    private HttpClient _admin = null!;

    protected override bool UseTestSignIn => false;

    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        [AdminBootstrap.EmailKey] = Email,
        [AdminBootstrap.PasswordKey] = Password,
    };

    [OneTimeSetUp]
    public async Task SignInAsync() => _admin = await AuthClient.SignedInAsync(Host, Email, Password);

    [OneTimeTearDown]
    public void DisposeAdmin() => _admin?.Dispose();

    [TestCase("/api/instance/settings")]
    [TestCase("/api/instance/integrations")]
    [Order(1)]
    public async Task InstanceBootstrap_WhenUnseeded_ShouldServeSettings(string path)
    {
        using var response = await _admin.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    [Order(2)]
    public async Task InstanceBootstrap_WhenHostHeaderChanges_ShouldKeepBaseUrlUnconfigured()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/instance/settings");
        request.Headers.Host = "untrusted.example.test";

        using var response = await _admin.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var settings = await response.Content.ReadFromJsonAsync<InstanceSettingsDto>(AuthClient.Json);
        settings.ShouldBe(new InstanceSettingsDto("ShipLean", "", ProjectCreation.Everyone));
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<string>("SELECT base_url FROM instance_settings")).ShouldBeEmpty();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM instance_settings")).ShouldBe(1);
    }

    [Test]
    [Order(3)]
    public async Task InstanceBootstrap_WhenStartedAgain_ShouldPreserveAllSavedSettings()
    {
        using var settings = await _admin.PutAsJsonAsync("/api/instance/settings", new
        {
            name = "Configured instance", baseUrl = "https://configured.example.test", projectCreation = "admins",
        });
        settings.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var integrations = await _admin.PutAsJsonAsync("/api/instance/integrations", new
        {
            gitHubToken = "ghp_bootstrap1234", gitLabUrl = "https://gitlab.example.test",
            gitLabToken = "glpat-bootstrap5678", previewAllowHosts = new[] { "docs.example.test" },
        });
        integrations.StatusCode.ShouldBe(HttpStatusCode.OK);
        await using var connection = await OpenDatabaseAsync();
        var before = await connection.QuerySingleAsync<string>("SELECT row_to_json(s)::text FROM instance_settings s");

        await using var restarted = ShipLeanHost.Start(ConnectionString, user: null, settings: HostSettings);
        using var admin = await AuthClient.SignedInAsync(restarted, Email, Password);
        using var readSettings = await admin.GetAsync("/api/instance/settings");
        using var readIntegrations = await admin.GetAsync("/api/instance/integrations");

        readSettings.StatusCode.ShouldBe(HttpStatusCode.OK);
        readIntegrations.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await readSettings.Content.ReadAsStringAsync()).ShouldBe(await settings.Content.ReadAsStringAsync());
        (await readIntegrations.Content.ReadAsStringAsync()).ShouldBe(await integrations.Content.ReadAsStringAsync());
        (await connection.QuerySingleAsync<string>("SELECT row_to_json(s)::text FROM instance_settings s")).ShouldBe(before);
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM instance_settings")).ShouldBe(1);
    }
}
