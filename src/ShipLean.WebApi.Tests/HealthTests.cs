using System.Net;
using System.Net.Http.Json;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Dtos;

namespace ShipLean.WebApi.Tests;

[TestFixture]
public class HealthTests : ApiFixture
{
    [Test]
    public async Task Health_ShouldAnswer200()
    {
        var response = await Client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<HealthDto>();
        body!.Status.ShouldBe("ok");
        body.Version.ShouldNotBeEmpty();
    }

    [Test]
    public async Task ApiHealth_ShouldAnswer200WithTheSamePayload()
    {
        var response = await Client.GetAsync("/api/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadFromJsonAsync<HealthDto>())!.Status.ShouldBe("ok");
    }

    [Test]
    public async Task UnknownApiRoute_WhenRequested_ShouldReturn404RatherThanTheSpaShell()
    {
        var response = await Client.GetAsync("/api/does-not-exist");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
