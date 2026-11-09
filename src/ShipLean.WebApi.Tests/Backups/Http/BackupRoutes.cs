using System.Text.Json.Nodes;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.Backups.Http;

// The five backup routes with a body that binds, for the access tests and the sweep rows.
public static class BackupRoutes
{
    public const string SaveBody = """
        {"enabled":true,"destination":{"provider":"s3","s3":{"region":"us-east-1","endpointUrl":null,"bucket":"shiplean-backups","prefix":"shiplean/","forcePathStyle":false,"accessKeyId":null,"secretKey":null}},"cron":"0 2 * * *","keepCount":14,"includeAttachments":false,"passphrase":"correct horse battery staple"}
        """;

    public const string DestinationBody = """
        {"provider":"s3","s3":{"region":"us-east-1","endpointUrl":null,"bucket":"shiplean-backups","prefix":"shiplean/","forcePathStyle":false,"accessKeyId":null,"secretKey":null}}
        """;

    public const string CronBody = """{"cron":"0 2 * * *"}""";

    public static IReadOnlyList<(string Method, string Url, string? Body)> All { get; } =
    [
        ("GET", "api/instance/backups", null),
        ("PUT", "api/instance/backups", SaveBody),
        ("POST", "api/instance/backups/test", DestinationBody),
        ("POST", "api/instance/backups/cron-preview", CronBody),
        ("POST", "api/instance/backups/run", null),
    ];

    public static Task<HttpResponseMessage> SendAsync(ApiClient api, string method, string url, string? body)
    {
        var json = body is null ? null : JsonNode.Parse(body);
        return method switch
        {
            "GET" => api.GetAsync(url),
            "PUT" => api.PutJsonAsync(url, json),
            _ => api.PostJsonAsync(url, json),
        };
    }
}
