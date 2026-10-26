using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests;

[TestFixture]
public class RequestOriginApiTests : ApiFixture
{
    // The test controller exists only in this test host.
    protected override Action<IServiceCollection>? ConfigureTestServices => services
        => services.AddControllers().AddApplicationPart(typeof(RequestOriginApiTests).Assembly);

    [Test]
    public async Task RequestOrigin_WhenTheRequestCarriesTheClientHeader_ShouldResolveIt()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/test-controller/things/origin");
        request.Headers.Add(ClientHeader.Name, "tab-42");

        var response = await Client.SendAsync(request);

        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("clientId").GetString().ShouldBe("tab-42");
    }

    [Test]
    public async Task RequestOrigin_WhenTheHeaderIsAbsent_ShouldBeNull()
    {
        var echo = await Client.GetFromJsonAsync<JsonElement>("/api/test-controller/things/origin");

        echo.GetProperty("clientId").ValueKind.ShouldBe(JsonValueKind.Null);
    }
}
