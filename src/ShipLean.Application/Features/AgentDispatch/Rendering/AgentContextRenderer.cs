using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// Pure: plain inputs in, Linear `AgentSessionEvent` bodies out. B3 adds webhookTimestamp and webhookId at send time.
//
// Field names are copied from Linear's AgentSessionEventWebhookPayload as in linear/linear
// packages/sdk/src/schema.graphql at commit 618a737a4e6aa279b7853c650f79f209a5142bbc (master on 2026-10-28,
// @linear/sdk package.json 99.0.0). Linear marks agent signals and plans as technology preview: re-check on bump.
// Used from the schema: AgentSessionEventWebhookPayload, AgentSessionWebhookPayload (issue is
// IssueWithDescriptionChildWebhookPayload), AgentActivityWebhookPayload, UserChildWebhookPayload,
// TeamChildWebhookPayload. Linear's issue has no parent, labels or project, so ShipLean adds `parent`, `labels` and
// `project` to it as extension fields (receivers written for Linear ignore them; promptContext carries the same).
// `promptContext` is sent on `created` only, as in Linear.
public static class AgentContextRenderer
{
    public const string EventType = "AgentSessionEvent";

    private static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static JsonObject RenderCreated(AgentContextInput input)
    {
        var body = Envelope("created", input, input.SessionCreatedAt, AgentSessionStatuses.Pending, input.SessionCreatedAt);
        body["promptContext"] = AgentPromptContext.Render(input);
        return body;
    }

    // sessionStatus is the session's Linear status when the prompt was sent (AgentSessionStatuses) and sessionUpdatedAt when it
    // last changed; the defaults are a pending session updated by the prompt itself.
    public static JsonObject RenderPrompted(
        AgentContextInput input, AgentPromptInput prompt, string sessionStatus = AgentSessionStatuses.Pending, DateTimeOffset? sessionUpdatedAt = null)
    {
        var body = Envelope("prompted", input, prompt.CreatedAt, sessionStatus, sessionUpdatedAt ?? prompt.CreatedAt);
        body["agentActivity"] = Activity(input, prompt);
        return body;
    }

    public static string ToJson(JsonObject body) => body.ToJsonString(Indented);

    private static JsonObject Envelope(string action, AgentContextInput input, DateTimeOffset at, string status, DateTimeOffset updatedAt) => new()
    {
        ["action"] = action,
        ["type"] = EventType,
        ["createdAt"] = Time(at),
        ["organizationId"] = input.Project.Id,
        ["appUserId"] = input.AppUserId,
        ["oauthClientId"] = input.AppUserId,
        ["agentSession"] = Session(input, status, updatedAt),
        ["guidance"] = Guidance(input),
    };

    // Linear's GuidanceRuleWebhookPayload { body, origin }: one rule, from the project (team).
    private static JsonArray Guidance(AgentContextInput input) => string.IsNullOrWhiteSpace(input.Guidance)
        ? new JsonArray()
        : new JsonArray(new JsonObject
        {
            ["body"] = input.Guidance,
            ["origin"] = new JsonObject
            {
                ["type"] = "team",
                ["team"] = new JsonObject { ["id"] = input.Project.Id, ["key"] = input.Project.Key, ["name"] = input.Project.Name },
            },
        });

    private static JsonObject Session(AgentContextInput input, string status, DateTimeOffset updatedAt) => new()
    {
        ["id"] = input.SessionId,
        ["appUserId"] = input.AppUserId,
        ["organizationId"] = input.Project.Id,
        ["createdAt"] = Time(input.SessionCreatedAt),
        ["updatedAt"] = Time(updatedAt),
        ["status"] = AgentSessionStatuses.Require(status),
        ["type"] = "commentThread",
        ["creatorId"] = input.Creator.Id,
        ["creator"] = User(input.Creator),
        ["issueId"] = input.Item.Id,
        ["issue"] = Issue(input),
    };

    private static JsonObject Issue(AgentContextInput input)
    {
        var item = input.Item;
        var issue = new JsonObject
        {
            ["id"] = item.Id,
            ["identifier"] = item.Key,
            ["title"] = item.Title,
            ["description"] = AgentDescription.WithCriteria(item.Description, item.Criteria),
            ["teamId"] = input.Project.Id,
            ["team"] = new JsonObject { ["id"] = input.Project.Id, ["key"] = input.Project.Key, ["name"] = input.Project.Name },
            ["url"] = item.Url,
            ["labels"] = new JsonArray(item.Tags.Select(tag => (JsonNode)new JsonObject { ["id"] = tag, ["name"] = tag }).ToArray()),
        };
        if (input.Parent is { } parent)
        {
            issue["parent"] = Related(parent);
        }
        if (input.Feature is { } feature)
        {
            issue["project"] = new JsonObject { ["id"] = feature.Id, ["name"] = feature.Title, ["url"] = feature.Url };
        }
        return issue;
    }

    private static JsonObject Related(AgentRelatedItemInput item) => new()
    {
        ["id"] = item.Id,
        ["identifier"] = item.Key,
        ["title"] = item.Title,
        ["description"] = item.Description,
        ["url"] = item.Url,
    };

    private static JsonObject Activity(AgentContextInput input, AgentPromptInput prompt)
    {
        var activity = new JsonObject
        {
            ["id"] = prompt.ActivityId,
            ["agentSessionId"] = input.SessionId,
            ["content"] = new JsonObject { ["type"] = "prompt", ["body"] = prompt.Body },
            ["createdAt"] = Time(prompt.CreatedAt),
            ["updatedAt"] = Time(prompt.CreatedAt),
            ["userId"] = prompt.Author.Id,
            ["user"] = User(prompt.Author),
        };
        if (prompt.CommentId != null)
        {
            activity["sourceCommentId"] = prompt.CommentId;
        }
        if (prompt.Signal != null)
        {
            activity["signal"] = prompt.Signal;
        }
        return activity;
    }

    // ShipLean has no page for another person, so UserChildWebhookPayload.url (String!) is empty.
    private static JsonObject User(AgentUserInput user) => new() { ["id"] = user.Id, ["name"] = user.Name, ["email"] = user.Email, ["url"] = string.Empty };

    private static string Time(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);
}
