using System.Text.Json;
using HotChocolate.Resolvers;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.LinearApi;

// A mutation's `input` argument read as JSON, whatever mix of dictionaries and JsonElements HotChocolate hands over (the JSON
// scalars arrive as elements), into the shapes IAgentReports takes.
internal static class LinearInput
{
    public static JsonElement Of(IResolverContext context, string name = "input")
        => JsonSerializer.SerializeToElement(context.ArgumentValue<object?>(name));

    public static string? Text(JsonElement input, string name)
        => input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    public static bool Has(JsonElement input, string name)
        => input.ValueKind == JsonValueKind.Object && input.TryGetProperty(name, out var value) && value.ValueKind is not JsonValueKind.Null;

    public static Guid SessionId(string? id)
        => Guid.TryParse(id, out var parsed) ? parsed : throw LinearErrors.NotFound("AgentSession");

    public static AgentActivityCreate Activity(JsonElement input)
    {
        var sessionId = SessionId(Text(input, "agentSessionId"));
        if (!input.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Object)
        {
            throw LinearErrors.Invalid("content must be an object with a type.");
        }
        Guid? id = null;
        if (Text(input, "id") is { Length: > 0 } given)
        {
            id = Guid.TryParse(given, out var parsed) ? parsed : throw LinearErrors.Invalid("id must be a UUID.");
        }

        return new AgentActivityCreate(
            sessionId, Text(content, "type") ?? string.Empty, Text(content, "body"), Text(content, "action"), Text(content, "parameter"), Text(content, "result"),
            input.TryGetProperty("ephemeral", out var ephemeral) && ephemeral.ValueKind == JsonValueKind.True, Text(input, "signal"), id);
    }

    public static AgentSessionChange Change(JsonElement input)
    {
        IReadOnlyList<AgentExternalUrl>? added = Array(input, "addedExternalUrls") is { } adds ? Urls(adds) : null;
        if (Text(input, "externalLink") is { Length: > 0 } link)
        {
            added = [.. added ?? [], new AgentExternalUrl("Link", link)];
        }

        // Linear types `plan` as a JSON object but sends the list of steps.
        IReadOnlyList<AgentPlanStep>? plan = null;
        if (Has(input, "plan"))
        {
            var steps = Array(input, "plan") ?? throw LinearErrors.Invalid("plan must be a list of {content, status} steps.");
            plan = [.. steps.EnumerateArray().Select(e => new AgentPlanStep(Text(e, "content") ?? string.Empty, Text(e, "status") ?? string.Empty))];
        }

        return new AgentSessionChange(
            Array(input, "externalUrls") is { } urls ? Urls(urls) : null,
            added,
            Array(input, "removedExternalUrls") is { } removed ? [.. removed.EnumerateArray().Where(e => e.ValueKind == JsonValueKind.String).Select(e => e.GetString()!)] : null,
            plan);
    }

    private static JsonElement? Array(JsonElement input, string name)
        => Has(input, name) && input.GetProperty(name).ValueKind == JsonValueKind.Array ? input.GetProperty(name) : null;

    private static AgentExternalUrl[] Urls(JsonElement array)
        => [.. array.EnumerateArray().Select(e => new AgentExternalUrl(Text(e, "label") ?? string.Empty, Text(e, "url") ?? string.Empty))];

    // The Linear error for what IAgentReports refused.
    public static async ValueTask<T> Guarded<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (ApiException refused) when (refused.Code == ApiErrorCodes.NotFound)
        {
            throw LinearErrors.NotFound(refused.Message.Replace(" not found.", string.Empty, StringComparison.Ordinal));
        }
        catch (ApiException refused) when (refused.Code == ApiErrorCodes.Validation)
        {
            throw LinearErrors.Invalid(refused.Message);
        }
    }
}
