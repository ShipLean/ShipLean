using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Instance.Integrations;

// A GitHub token is only sent to an https API address, so saving one while SHIPLEAN_GITHUB_API_URL is plain http is refused.
[TestFixture]
public class IntegrationsGitHubHttpTests : ApiFixture
{
    protected override IReadOnlyDictionary<string, string?> HostSettings => new Dictionary<string, string?>
    {
        ["SHIPLEAN_GITHUB_API_URL"] = "http://ghe.example.com/api/v3",
    };

    [Test]
    public async Task Integrations_WhenGitHubApiUrlIsNotHttps_ShouldRefuseAGitHubTokenWithoutEchoingIt()
    {
        using var response = await Api.PutJsonAsync("api/instance/integrations", new { gitHubToken = "ghp_leaky00009999" });

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("githubApiUrl");
        error.Error.ShouldNotContain("leaky");
        (await IntegrationsWire.GetAsync(Api)).GitHubTokenLastFour.ShouldBeNull();
    }
}
