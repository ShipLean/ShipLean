using System.Net;
using System.Text;
using System.Text.Json;

namespace ShipLean.UnitTest.Agents.Clients;

internal static class MetadataDocuments
{
    public const string Url = "https://client.example.test/oauth/metadata.json";

    public static string Json(string? clientId = Url, Dictionary<string, object?>? extra = null)
    {
        var document = new Dictionary<string, object?>
        {
            ["client_id"] = clientId,
            ["client_name"] = "Review Bot",
            ["redirect_uris"] = new[] { "http://127.0.0.1:8123/callback", "https://client.example.test/callback" },
        };
        foreach (var (key, value) in extra ?? [])
        {
            document[key] = value;
        }
        return JsonSerializer.Serialize(document);
    }

    public static HttpResponseMessage Ok(string json) => new(HttpStatusCode.OK)
    {
        Content = new StringContent(json, Encoding.UTF8, "application/json"),
    };
}
