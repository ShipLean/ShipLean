using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using ShipLean.TestSupport;

namespace ShipLean.WebApi.Tests.LinearApi;

// Posts the raw documents @linear/sdk sends (Fixtures/linear-sdk, from the pinned _generated_documents.ts) and reads Linear's
// response shape.
internal static class LinearGraphQl
{
    public const string Route = "linear/graphql";

    public static string Document(string name)
        => File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.WebApi.Tests", "Fixtures", "linear-sdk", name + ".graphql"));

    public static async Task<(HttpStatusCode Status, JsonNode Body)> PostAsync(HttpClient client, string? authorization, string document, object? variables = null)
    {
        var body = new JsonObject { ["query"] = document, ["variables"] = variables is null ? null : JsonNode.Parse(System.Text.Json.JsonSerializer.Serialize(variables)) };
        using var request = new HttpRequestMessage(HttpMethod.Post, Route) { Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json") };
        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }
        using var response = await client.SendAsync(request);
        var text = await response.Content.ReadAsStringAsync();
        return (response.StatusCode, text.Length == 0 ? new JsonObject() : JsonNode.Parse(text)!);
    }

    // The first error's extensions.code, or null.
    public static string? ErrorCode(JsonNode body) => body["errors"]?[0]?["extensions"]?["code"]?.GetValue<string>();
}
