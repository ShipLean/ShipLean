using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Agents.Test;

// Test connection: a signed ping with no item data, sent with the deliverer's signing and named HttpClient (5 s timeout, no redirects).
// Any HTTP answer counts as reached, because even a 4xx for a type the relay does not know proves the address and the signature got there.
// Nothing is stored: a test is not a delivery attempt.
public sealed class AgentConnectionTester(IAgentConnections connections, IHttpClientFactory httpClients, TimeProvider time) : IAgentConnectionTester
{
    public const string PingType = "ping";

    public async Task<AgentConnectionTestDto> TestAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var agent = await connections.GetAsync(connectionId, cancellationToken) ?? throw ApiException.NotFound("That agent connection doesn't exist.");
        if (agent.ArchivedAt is not null)
        {
            throw ApiException.Validation("That agent connection is archived. Restore it before you test it.");
        }

        if (!Uri.TryCreate(agent.ReceiverUrl, UriKind.Absolute, out var url) || url.Scheme is not ("http" or "https"))
        {
            return new AgentConnectionTestDto(false, null, "The receiver URL is not an http(s) address.", 0);
        }

        var started = time.GetTimestamp();
        try
        {
            using var request = Ping(agent, url);
            using var response = await httpClients.CreateClient(AgentDeliverer.HttpClientName).SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return new AgentConnectionTestDto(true, (int)response.StatusCode, null, Elapsed(started));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return new AgentConnectionTestDto(false, null, "No answer within 5 seconds.", Elapsed(started));
        }
        catch (HttpRequestException failed)
        {
            return new AgentConnectionTestDto(false, null, AgentErrorText.Scrub($"{failed.GetType().Name}: {failed.Message}"), Elapsed(started));
        }
    }

    private int Elapsed(long started) => (int)time.GetElapsedTime(started).TotalMilliseconds;

    // The body holds the type and the signing fields only: no item id, key, title or session.
    private HttpRequestMessage Ping(AgentConnection agent, Uri url)
    {
        var sentAt = time.GetUtcNow().ToUnixTimeMilliseconds();
        var body = new JsonObject { ["type"] = PingType, ["webhookTimestamp"] = sentAt, ["webhookId"] = agent.Id.ToString() };
        var bytes = Encoding.UTF8.GetBytes(body.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }));

        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        request.Headers.AcceptCharset.Add(new StringWithQualityHeaderValue("utf-8"));
        request.Headers.TryAddWithoutValidation("Linear-Delivery", Guid.CreateVersion7().ToString());
        request.Headers.TryAddWithoutValidation("Linear-Event", PingType);
        request.Headers.TryAddWithoutValidation(LinearSignature.Header, LinearSignature.Sign(agent.SigningSecret, bytes));
        request.Headers.TryAddWithoutValidation("Linear-Timestamp", sentAt.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.UserAgent.ParseAdd("Linear-Webhook");
        return request;
    }
}
