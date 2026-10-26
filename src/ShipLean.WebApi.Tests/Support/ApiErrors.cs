using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Tests;

// The one way an API test reads an error: the body must be { code, error } and, when a field is at fault, a string field, with a sentence in error.
public static class ApiErrors
{
    public static async Task<ApiError> ReadApiErrorAsync(this HttpResponseMessage response)
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

    public static async Task<ApiError> ReadApiErrorAsync(this HttpResponseMessage response, HttpStatusCode status)
    {
        response.StatusCode.ShouldBe(status);
        return await response.ReadApiErrorAsync();
    }
}
