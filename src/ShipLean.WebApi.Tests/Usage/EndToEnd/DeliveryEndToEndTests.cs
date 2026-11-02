using System.Net;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Analytics;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Usage;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Analytics;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Usage.EndToEnd;

// From a real capture (an MCP tool call, a web write) through the real queue, dispatcher and mapper and the real Umami and PostHog sinks,
// to the exact HTTP request the provider would get. Only the HTTP stub and the clock are doubles.
public static class DeliveryEndToEndTests
{
    private const string UmamiKey = "7f3c1a52-9d4e-4b8a-a1c6-2e5f0b9d3c71";
    private const string PostHogKey = "phc_abcdefghijklmnop";

    private static IEnumerable<JsonNode> BodyItems(AnalyticsSinkStubHandler stub)
    {
        foreach (var request in stub.Requests)
        {
            var node = JsonNode.Parse(request.Body)!;
            var items = node is JsonArray array ? array : node["batch"]!.AsArray();
            foreach (var item in items)
            {
                yield return item!;
            }
        }
    }

    private static async Task SaveAsync(ShipLeanHost host, AnalyticsProvider provider, string? url, string? key)
    {
        await using var scope = host.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<AnalyticsSettings>().SaveAsync(new AnalyticsSettingsDto(provider, url, key, false), CancellationToken.None);
    }

    [TestFixture]
    public class McpToUmami : McpFixture
    {
        private readonly EndToEndAnalytics _analytics = new();

        protected override bool WithProbeTools => false;

        protected override Action<IServiceCollection>? ConfigureTestServices => services =>
        {
            base.ConfigureTestServices?.Invoke(services);
            _analytics.Register(services);
        };

        [OneTimeTearDown]
        public void DisposeAnalytics() => _analytics.Dispose();

        [Test]
        public async Task McpCaptureWork_WhenUmamiConfigured_ShouldPostIdentifyAndEventWithProjectPathAndAgentTag()
        {
            const string clientName = "E2E Agent";
            await SaveAsync(Host, AnalyticsProvider.Umami, "https://umami.example.com", UmamiKey);
            var token = await TokenAsync(TestUser.Joel, ReadWrite, clientName);
            await using var scope = Host.Services.CreateAsyncScope();
            var connected = await scope.ServiceProvider.GetRequiredService<IAgentAuthorizations>().ListAsync(TestUser.Joel.Id!.Value);
            var authorizationId = connected.Single(a => a.ClientName == clientName).Id;

            var reply = await CallToolAsync(token, "capture_work", """{"project":"SHIP","items":[{"type":"task","title":"End to end"}]}""");
            reply.IsToolError().ShouldBeFalse();
            await _analytics.AdvanceUntilAsync(() => BodyItems(_analytics.Stub).Any(i => i["type"]!.GetValue<string>() == "event"));

            var request = _analytics.Stub.Requests.Single(r => r.Body.Contains("work.captured", StringComparison.Ordinal));
            request.Method.ShouldBe(HttpMethod.Post);
            request.Url.ToString().ShouldBe("https://umami.example.com/api/batch");
            request.Headers["User-Agent"].ShouldStartWith("ShipLean/");
            var items = JsonNode.Parse(request.Body)!.AsArray();
            var distinctId = $"agent:{authorizationId}";
            var identify = items.Single(i => i!["type"]!.GetValue<string>() == "identify")!["payload"]!;
            identify["website"]!.GetValue<string>().ShouldBe(UmamiKey);
            identify["id"]!.GetValue<string>().ShouldBe(distinctId);
            identify["data"]!["kind"]!.GetValue<string>().ShouldBe("agent");
            identify["data"]!["agent_name"]!.GetValue<string>().ShouldBe(clientName);
            var capture = items.Single(i => i!["payload"]!["name"]?.GetValue<string>() == UsageEventNames.WorkCaptured)!["payload"]!;
            capture["website"]!.GetValue<string>().ShouldBe(UmamiKey);
            capture["url"]!.GetValue<string>().ShouldBe("/p/SHIP/mcp/work/captured");
            capture["title"]!.GetValue<string>().ShouldBe(UsageEventNames.WorkCaptured);
            capture["id"]!.GetValue<string>().ShouldBe(distinctId);
            capture["tag"]!.GetValue<string>().ShouldBe("agent");
            capture["data"]!["project_key"]!.GetValue<string>().ShouldBe("SHIP");
            capture["data"]!["project_id"]!.GetValue<string>().ShouldBe(SeedIds.Projects.Ship.ToString());
            capture["data"]!["agent_name"]!.GetValue<string>().ShouldBe(clientName);
            capture["data"]!["surface"]!.GetValue<string>().ShouldBe("mcp");
            capture["data"]!["source"]!.GetValue<string>().ShouldBe("tool:capture_work");
            capture["data"]!["outcome"]!.GetValue<string>().ShouldBe("ok");
            items.IndexOf(items.Single(i => i!["type"]!.GetValue<string>() == "identify")).ShouldBeLessThan(items.IndexOf(items.Single(i => i!["payload"]!["name"]?.GetValue<string>() == UsageEventNames.WorkCaptured)));
        }
    }

    [TestFixture]
    public class WebToPostHog : ApiFixture
    {
        private readonly EndToEndAnalytics _analytics = new();

        protected override Action<IServiceCollection>? ConfigureTestServices => _analytics.Register;

        [OneTimeTearDown]
        public void DisposeAnalytics() => _analytics.Dispose();

        [Test]
        public async Task WebCreateWorkItem_WhenPostHogConfigured_ShouldPostEventWithGroupsProjectAndUserDistinctId()
        {
            using (var put = await Api.PutJsonAsync("api/instance/analytics", new { provider = "posthog", host = "https://posthog.example.com", siteKey = PostHogKey, sendNames = false }))
            {
                put.StatusCode.ShouldBe(HttpStatusCode.OK, await put.Content.ReadAsStringAsync());
            }

            using var create = await Api.PostJsonAsync("api/work-items", new JsonObject
            {
                ["projectId"] = SeedIds.Projects.Ship,
                ["type"] = "story",
                ["title"] = "End to end create",
                ["parentId"] = null,
                ["sprintId"] = null,
                ["changes"] = null,
            });
            create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());
            await _analytics.AdvanceUntilAsync(() => BodyItems(_analytics.Stub).Any(i => i["event"]!.GetValue<string>() == UsageEventNames.WorkItemCreated));

            var request = _analytics.Stub.Requests.Single(r => r.Body.Contains(UsageEventNames.WorkItemCreated, StringComparison.Ordinal));
            request.Method.ShouldBe(HttpMethod.Post);
            request.Url.ToString().ShouldBe("https://posthog.example.com/batch/");
            var body = JsonNode.Parse(request.Body)!;
            body["api_key"]!.GetValue<string>().ShouldBe(PostHogKey);
            body["historical_migration"]!.GetValue<bool>().ShouldBeFalse();
            var created = body["batch"]!.AsArray().Single(i => i!["event"]!.GetValue<string>() == UsageEventNames.WorkItemCreated)!;
            created["distinct_id"]!.GetValue<string>().ShouldBe($"user:{SeedIds.Users.Joel}");
            var properties = created["properties"]!;
            properties["$lib"]!.GetValue<string>().ShouldBe("shiplean");
            properties["$groups"]!["project"]!.GetValue<string>().ShouldBe(SeedIds.Projects.Ship.ToString());
            properties["project_key"]!.GetValue<string>().ShouldBe("SHIP");
            properties["surface"]!.GetValue<string>().ShouldBe("web");
            properties["actor_kind"]!.GetValue<string>().ShouldBe("user");
            properties["user_id"]!.GetValue<string>().ShouldBe(SeedIds.Users.Joel.ToString());
            properties["$set"]!["kind"]!.GetValue<string>().ShouldBe("user");
            properties["$set"]!["user_id"]!.GetValue<string>().ShouldBe(SeedIds.Users.Joel.ToString());
            properties["$set"]!["name"].ShouldBeNull();
            properties["$set"]!["email"].ShouldBeNull();
        }
    }

    [TestFixture]
    public class ProviderOff : ApiFixture
    {
        private readonly EndToEndAnalytics _analytics = new();

        protected override Action<IServiceCollection>? ConfigureTestServices => _analytics.Register;

        [OneTimeTearDown]
        public void DisposeAnalytics() => _analytics.Dispose();

        [Test]
        public async Task ProviderNone_ShouldPostNothing()
        {
            using var create = await Api.PostJsonAsync("api/work-items", new JsonObject
            {
                ["projectId"] = SeedIds.Projects.Ship,
                ["type"] = "story",
                ["title"] = "Off create",
                ["parentId"] = null,
                ["sprintId"] = null,
                ["changes"] = null,
            });
            create.StatusCode.ShouldBe(HttpStatusCode.OK, await create.Content.ReadAsStringAsync());

            // The dispatcher has read the settings (provider none) for the batch holding the create, and dropped it.
            await _analytics.ReadSettingsAsync();
            _analytics.Stub.Requests.ShouldBeEmpty();

            // The same pipeline still delivers once a provider is set: the only request carries the later event, not the dropped one.
            await SaveAsync(Host, AnalyticsProvider.Umami, "https://umami.example.com", UmamiKey);
            _analytics.ExpireSettingsCache();
            using var second = await Api.PostJsonAsync("api/work-items", new JsonObject
            {
                ["projectId"] = SeedIds.Projects.Ship,
                ["type"] = "story",
                ["title"] = "On create",
                ["parentId"] = null,
                ["sprintId"] = null,
                ["changes"] = null,
            });
            second.StatusCode.ShouldBe(HttpStatusCode.OK, await second.Content.ReadAsStringAsync());
            await _analytics.AdvanceUntilAsync(() => BodyItems(_analytics.Stub).Any(i => i["type"]!.GetValue<string>() == "event"));

            var events = BodyItems(_analytics.Stub).Where(i => i["type"]!.GetValue<string>() == "event").ToList();
            events.Count.ShouldBe(1);
            events[0]["payload"]!["name"]!.GetValue<string>().ShouldBe(UsageEventNames.WorkItemCreated);
        }
    }
}
