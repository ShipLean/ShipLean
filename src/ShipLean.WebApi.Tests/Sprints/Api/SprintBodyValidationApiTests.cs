using System.Net;
using System.Text;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Sprints.Api;

// Bodies with missing, null or over-precise fields are 400 validation, never 500 (the framework doesn't enforce required members).
[TestFixture]
public class SprintBodyValidationApiTests : ApiFixture
{
    private async Task<ApiError> SendAsync(HttpMethod method, string path, string json)
    {
        using var request = new HttpRequestMessage(method, path) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        request.Headers.Add(ShipLean.Contracts.ClientHeader.Name, ApiClient.ClientId);
        using var response = await Client.SendAsync(request);
        return await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);
    }

    [TestCase("rollover", "{}")]
    [TestCase("rollover", """{"moves":null}""")]
    [TestCase("rollover", """{"moves":[null]}""")]
    [TestCase("capacity", "{}")]
    [TestCase("capacity", """{"members":null}""")]
    [TestCase("capacity", """{"members":[null]}""")]
    [TestCase("capacity", """{"members":[{"userId":"USER","points":4.555}]}""")]
    [TestCase("forecast", """{"velocityOverride":12.345}""")]
    [TestCase("create", """{"name":"No dates"}""")]
    [TestCase("create", """{"name":"No end","startDate":"2040-01-01"}""")]
    [TestCase("update", """{"startDate":"0001-01-01"}""")]
    [TestCase("update", """{"endDate":"0001-01-01"}""")]
    public async Task Body_WhenFieldsMissingOrImprecise_ShouldBeValidation(string route, string json)
    {
        json = json.Replace("USER", SeedIds.Users.Priya.ToString());
        var (method, path) = route switch
        {
            "rollover" => (HttpMethod.Post, $"api/sprints/{SeedIds.Sprints.Sprint19}/rollover"),
            "capacity" => (HttpMethod.Put, $"api/sprints/{SeedIds.Sprints.Sprint26}/capacity"),
            "forecast" => (HttpMethod.Put, $"api/projects/{SeedIds.Projects.Ship}/forecast"),
            "create" => (HttpMethod.Post, $"api/projects/{SeedIds.Projects.Ship}/sprints"),
            _ => (HttpMethod.Patch, $"api/sprints/{SeedIds.Sprints.Sprint26}"),
        };

        var error = await SendAsync(method, path, json);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
    }
}
