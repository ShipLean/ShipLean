using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Backups.Http;

// Who may call the five backup routes, whatever IBackupAdmin answers: only an instance admin. Each route is asserted
// on its own, so one route losing [InstanceAdmin] names itself.
[TestFixture]
public class BackupsApiAccessTests : ApiFixture
{
    [Test]
    public async Task Routes_WhenNotAdmin_ShouldReturnForbidden()
    {
        var mei = Api.ActingAs(TestUser.Mei);
        var wrong = new List<string>();
        foreach (var (method, url, body) in BackupRoutes.All)
        {
            using var response = await BackupRoutes.SendAsync(mei, method, url, body);
            if (response.StatusCode != HttpStatusCode.Forbidden)
            {
                wrong.Add($"{method} {url}: {(int)response.StatusCode}");
                continue;
            }
            (await ApiClient.ReadApiErrorAsync(response)).Code.ShouldBe(ApiErrorCodes.Forbidden, $"{method} {url}");
        }

        wrong.ShouldBeEmpty();
    }

    [Test]
    public async Task Routes_WhenSignedOut_ShouldReturnUnauthorized()
    {
        var anonymous = Api.ActingAs(TestUser.Anonymous);
        var wrong = new List<string>();
        foreach (var (method, url, body) in BackupRoutes.All)
        {
            using var response = await BackupRoutes.SendAsync(anonymous, method, url, body);
            if (response.StatusCode != HttpStatusCode.Unauthorized)
            {
                wrong.Add($"{method} {url}: {(int)response.StatusCode}");
                continue;
            }
            (await ApiClient.ReadApiErrorAsync(response)).Code.ShouldBe(ApiErrorCodes.Unauthorized, $"{method} {url}");
        }

        wrong.ShouldBeEmpty();
    }
}
