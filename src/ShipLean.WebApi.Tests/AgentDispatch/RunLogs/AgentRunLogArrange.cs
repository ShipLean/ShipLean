using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.AgentDispatch.RunLogs;

// A project of its own (Joel admin, Priya member) with a connection and a repository, one item, and an agent session started through
// Start agent, so its `created` delivery carries the real agent-log link. Uploads go as an anonymous caller, as a relay's curl would.
internal sealed record AgentRunLogArrange(Guid ProjectId, Guid ItemId, string ItemKey, Guid SessionId, string Url, string Token)
{
    public const string LinePrefix = "If you end with an error, POST your last 200 lines of output as text/plain to ";

    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    public string PathAndQuery => new Uri(Url).PathAndQuery;

    public static async Task<AgentRunLogArrange> StartAsync(NpgsqlConnection database, ApiClient api)
    {
        var arranged = await AgentDispatchArrange.CreateAsync(database, api);
        await database.ExecuteAsync(
            "INSERT INTO project_member (id, project_id, user_id, role) VALUES (@id, @projectId, @userId, 'member')",
            new { id = Guid.NewGuid(), projectId = arranged.Project.Id, userId = SeedIds.Users.Priya });
        var item = await arranged.CreateItemAsync(api);
        using var started = await api.PostJsonAsync($"api/work-items/{item.Id}/agent-session");
        started.EnsureSuccessStatusCode();
        var delivery = (await AgentDispatchArrange.DeliveriesAsync(database, item.Id)).Single(d => d.Action == "created");
        var url = UrlIn(GuidanceOf(delivery));
        var token = Uri.UnescapeDataString(new Uri(url).Query["?token=".Length..]);
        return new AgentRunLogArrange(arranged.Project.Id, item.Id, item.Key, delivery.SessionId, url, token);
    }

    public static string GuidanceOf(AgentDispatchArrange.DeliveryRow delivery)
        => AgentDispatchArrange.BodyOf(delivery)["guidance"]![0]!["body"]!.GetValue<string>();

    public static string UrlIn(string guidance)
    {
        var line = guidance.Split('\n').Single(l => l.StartsWith(LinePrefix, StringComparison.Ordinal));
        return line[LinePrefix.Length..^1];
    }

    public static HttpRequestMessage Upload(string pathAndQuery, HttpContent content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, pathAndQuery) { Content = content };
        request.Headers.Add(TestAuthentication.UserHeader, TestAuthentication.AnonymousValue);
        return request;
    }

    public static StringContent Text(string text, string mediaType = "text/plain") => new(text, Encoding.UTF8, mediaType);

    // The body with no declared length (chunked), so only the streamed bytes can be counted.
    public static HttpContent Chunked(int bytes, string mediaType = "text/plain")
    {
        var content = new StreamContent(new MemoryStream(Enumerable.Repeat((byte)'x', bytes).ToArray()));
        content.Headers.ContentType = new MediaTypeHeaderValue(mediaType);
        content.Headers.ContentLength = null;
        return content;
    }

    public async Task EndAsFailedAsync(NpgsqlConnection database)
        => await database.ExecuteAsync(
            "UPDATE agent_session SET state = 'failed', end_reason = @reason, end_detail = 'dotnet build failed', ended_at = now() WHERE id = @id",
            new { id = SessionId, reason = AgentEndReasons.AgentError });

    public async Task<Guid?> RunLogIdAsync(NpgsqlConnection database)
        => await database.ExecuteScalarAsync<Guid?>("SELECT run_log_attachment_id FROM agent_session WHERE id = @id", new { id = SessionId });

    public async Task<AgentSessionDto> PanelSessionAsync(ApiClient api, TestUser user)
    {
        using var response = await api.ActingAs(user).GetAsync($"api/work-items/{ItemId}/agent-sessions");
        var text = await response.Content.ReadAsStringAsync();
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<AgentPanelDto>(text, Wire)!.Sessions.Single(s => s.Id == SessionId);
    }
}
