using ShipLean.AcceptanceTests.Harness;
using ShipLean.AcceptanceTests.Pages.Settings;

namespace ShipLean.AcceptanceTests.Lanes.Settings.Instance;

// An instance admin sets GitLab on the Integrations tab; after a reload the address is there and the token shows only its end.
[TestFixture]
public class IntegrationsTests : SeededAcceptanceTest
{
    [Test]
    public async Task Integrations_WhenGitLabSet_ShouldPersistAcrossReload()
    {
        var integrations = new InstanceIntegrationsPage(Page);
        await integrations.OpenAsync();

        await integrations.GitLabUrl.FillAsync("https://gitlab.larkspur.example.com");
        await integrations.GitLabTokenInput.FillAsync("glpat-larkspur-secret-9876");
        await integrations.SaveAsync();
        await Page.ReloadAsync();

        await Expect(integrations.GitLabUrl).ToHaveValueAsync("https://gitlab.larkspur.example.com");
        await Expect(integrations.GitLabTokenLastFour).ToHaveTextAsync("•••• 9876");
        await Expect(integrations.Panel).Not.ToContainTextAsync("larkspur-secret");
    }
}
