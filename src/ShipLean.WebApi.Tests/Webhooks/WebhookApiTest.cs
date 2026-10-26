using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Webhooks;
using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.TestSupport.Webhooks;

namespace ShipLean.WebApi.Tests.Webhooks;

// A webhook scenario: the host runs with the secrets HostSettings sets, the test arranges items in SHIP through the API as
// Joel, and posts recorded provider payloads as an anonymous caller, the way GitHub and GitLab do.
public abstract class WebhookApiTest : ApiFixture
{
    protected const string GithubSecret = "github-test-secret";
    protected const string GitlabSecret = "gitlab-test-secret";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false) },
    };

    // Every delivery is later than the one before, as a provider's are.
    private static readonly DateTimeOffset Start = new(2026, 10, 25, 9, 0, 0, TimeSpan.Zero);
    private int _deliveries;

    protected DateTimeOffset Next() => Start.AddMinutes(++_deliveries);

    protected async Task<WriteResult> CreateAsync(string type, string title)
    {
        using var response = await Api.PostJsonAsync("api/work-items", new JsonObject
        {
            ["projectId"] = SeedIds.Projects.Ship,
            ["type"] = type,
            ["title"] = title,
            ["parentId"] = null,
            ["sprintId"] = null,
        });
        return await ReadAsync<WriteResult>(response);
    }

    protected async Task LinkAsync(Guid id, string url)
    {
        var item = await GetAsync(id);
        using var response = await Api.PatchJsonAsync($"api/work-items/{id}", new JsonObject
        {
            ["expectedRevision"] = item.Revision,
            ["changes"] = new JsonObject
            {
                ["addLinks"] = new JsonArray(new JsonObject { ["type"] = "url", ["workItemId"] = null, ["key"] = null, ["url"] = url, ["title"] = null }),
            },
        });
        response.StatusCode.ShouldBe(HttpStatusCode.OK, await response.Content.ReadAsStringAsync());
    }

    protected async Task<WorkItemDto> GetAsync(Guid id)
    {
        using var response = await Api.GetAsync($"api/work-items/{id}");
        return await ReadAsync<WorkItemDto>(response);
    }

    protected async Task<HistoryDto> HistoryAsync(Guid id)
    {
        using var response = await Api.GetAsync($"api/work-items/{id}/history");
        return await ReadAsync<HistoryDto>(response);
    }

    protected static StateCategory CategoryOf(WorkItemDto item) => item.State.Category;

    protected static LinkDto UrlLink(WorkItemDto item) => item.Links.ShouldHaveSingleItem();

    protected Task<HttpResponseMessage> PostAsync(WebhookDelivery delivery) => Client.SendAsync(DeliveryRequest(delivery));

    protected static HttpRequestMessage DeliveryRequest(WebhookDelivery delivery)
        => delivery.WithHeader(TestAuthentication.UserHeader, TestAuthentication.AnonymousValue).ToRequest();

    protected async Task<WebhookResultDto> DeliverAsync(WebhookDelivery delivery)
    {
        using var response = await PostAsync(delivery);
        return await ReadAsync<WebhookResultDto>(response);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response, HttpStatusCode status = HttpStatusCode.OK)
    {
        var text = await response.Content.ReadAsStringAsync();
        response.StatusCode.ShouldBe(status, text);
        return JsonSerializer.Deserialize<T>(text, Wire)!;
    }
}
