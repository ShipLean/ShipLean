using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests;

// Enums cross the wire as camelCase names, the casing the persisted documents and the TypeScript unions use.
[TestFixture]
public class EnumWireTests : ApiFixture
{
    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        services.AddControllers().AddApplicationPart(typeof(EnumWireTests).Assembly);

    [Test]
    public async Task Response_WhenEnumReturned_ShouldBeCamelCase()
    {
        var json = await Client.GetStringAsync("/api/test-controller/things/category");

        json.ShouldBe("""{"category":"inProgress"}""");
    }

    [Test]
    public async Task Request_WhenEnumIsCamelCase_ShouldBind()
    {
        using var body = new StringContent("""{"category":"inProgress"}""", System.Text.Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/test-controller/things/category", body);

        (await response.Content.ReadAsStringAsync()).ShouldBe("""{"category":"inProgress"}""");
    }

    [Test]
    public async Task Request_WhenEnumIsANumber_ShouldBeValidation()
    {
        using var body = new StringContent("""{"category":1}""", System.Text.Encoding.UTF8, "application/json");

        var response = await Client.PostAsync("/api/test-controller/things/category", body);

        (await response.ReadApiErrorAsync(HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
