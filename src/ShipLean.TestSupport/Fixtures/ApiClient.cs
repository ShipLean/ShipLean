using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.Hosting;
using Shouldly;

namespace ShipLean.TestSupport.Fixtures;

// An HttpClient wrapper for API tests: every request carries ClientHeader and JSON bodies use the web defaults.
public sealed class ApiClient(HttpClient http, Guid? actingAs = null, bool anonymous = false)
{
    public const string ClientId = "api-test";

    public HttpClient Http => http;

    // The same connection, with its requests made as another seeded user (or nobody) instead of the fixture's.
    public ApiClient ActingAs(TestUser user) => new(http, user.Id, user.Id is null);

    public Task<HttpResponseMessage> GetAsync(string path) => SendAsync(HttpMethod.Get, path, null);

    // GET and deserialize; any status other than 2xx fails the test with the response body.
    public async Task<T> GetJsonAsync<T>(string path)
    {
        using var response = await GetAsync(path);
        var text = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"GET {path} answered {(int)response.StatusCode}: {text}", null, response.StatusCode);
        }
        return JsonSerializer.Deserialize<T>(text, JsonSerializerOptions.Web)!;
    }

    public Task<HttpResponseMessage> PostJsonAsync(string path, object? body = null) => SendAsync(HttpMethod.Post, path, body);

    public Task<HttpResponseMessage> PatchJsonAsync(string path, object? body = null) => SendAsync(HttpMethod.Patch, path, body);

    public Task<HttpResponseMessage> PutJsonAsync(string path, object? body = null) => SendAsync(HttpMethod.Put, path, body);

    public Task<HttpResponseMessage> DeleteAsync(string path) => SendAsync(HttpMethod.Delete, path, null);

    // The one way a test reads an error: the body must be { code, error } and, when a field is at fault, a string field, with a sentence in error.
    public static async Task<ApiError> ReadApiErrorAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.EnumerateObject().Select(p => p.Name).Where(name => name != "field").Order(StringComparer.Ordinal).ShouldBe(["code", "error"]);
        if (body.TryGetProperty("field", out var field))
        {
            field.ValueKind.ShouldBe(JsonValueKind.String);
        }
        var error = body.Deserialize<ApiError>(JsonSerializerOptions.Web)!;
        error.Error.ShouldNotBeNullOrEmpty();
        return error;
    }

    public static async Task<ApiError> ReadApiErrorAsync(HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        return await ReadApiErrorAsync(response);
    }

    private async Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, object? body)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Add(ClientHeader.Name, ClientId);
        if (anonymous || actingAs != null)
        {
            request.Headers.Add(TestAuthentication.UserHeader, anonymous ? TestAuthentication.AnonymousValue : actingAs.ToString());
        }
        if (body != null)
        {
            request.Content = JsonContent.Create(body, options: JsonSerializerOptions.Web);
        }
        return await http.SendAsync(request);
    }
}
