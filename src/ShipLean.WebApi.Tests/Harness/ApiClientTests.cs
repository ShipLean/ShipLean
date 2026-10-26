using System.Net;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Dtos;

namespace ShipLean.WebApi.Tests.Harness;

[TestFixture]
public class ApiClientTests : ApiFixture
{
    [Test]
    public async Task GetJsonAsync_WhenRouteExists_ShouldDeserialize()
    {
        var health = await Api.GetJsonAsync<HealthDto>("/api/health");

        health.Status.ShouldBe("ok");
    }

    [Test]
    public async Task ReadApiErrorAsync_WhenRouteIsUnknown_ShouldReturnTheNotFoundError()
    {
        using var response = await Api.GetAsync("/api/does-not-exist");

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
