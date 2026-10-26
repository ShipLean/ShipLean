using System.Net;
using ShipLean.TestSupport.Webhooks;

namespace ShipLean.WebApi.Tests.Webhooks;

// Neither SHIPLEAN_GITHUB_WEBHOOK_SECRET nor SHIPLEAN_GITLAB_WEBHOOK_SECRET is set: both endpoints don't exist.
[TestFixture]
public class WebhooksOffTests : WebhookApiTest
{
    [Test]
    public async Task GitHub_WhenSecretUnset_ShouldBeNotFound()
    {
        using var response = await PostAsync(WebhookDelivery.Github(GithubSecret, "opened", 1, "SHIP-1 A title", Next()));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task GitLab_WhenSecretUnset_ShouldBeNotFound()
    {
        using var response = await PostAsync(WebhookDelivery.Gitlab(GitlabSecret, "open", 1, "SHIP-1 A title", Next()));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
