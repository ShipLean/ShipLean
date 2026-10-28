using ShipLean.Application.Features.LinearApi;

namespace ShipLean.WebApi.Features.LinearApi;

// An activity row as Linear's AgentActivity. content is one of the AgentActivityContent union's members; its __typename is
// what the union resolution reads. ShipLean does not record who wrote a row, so `user` is the calling connection's app user.
internal static class LinearActivityMap
{
    public static IReadOnlyDictionary<string, object?> Of(LinearActivity activity, LinearCaller caller) => new Dictionary<string, object?>
    {
        ["id"] = activity.Id.ToString(),
        ["createdAt"] = activity.CreatedAt,
        ["updatedAt"] = activity.CreatedAt,
        ["ephemeral"] = activity.Ephemeral,
        ["signal"] = activity.Signal,
        ["content"] = Content(activity),
        ["agentSession"] = new Dictionary<string, object?> { ["id"] = activity.SessionId.ToString() },
        ["user"] = LinearPersonMap.App(caller),
    };

    private static Dictionary<string, object?> Content(LinearActivity activity)
    {
        var content = new Dictionary<string, object?> { ["type"] = activity.Type, ["body"] = activity.Body ?? string.Empty };
        switch (activity.Type)
        {
            case "action":
                content["__typename"] = "AgentActivityActionContent";
                content["action"] = activity.Action ?? string.Empty;
                content["parameter"] = activity.Parameter ?? string.Empty;
                content["result"] = activity.Result;
                break;
            case "elicitation":
                content["__typename"] = "AgentActivityElicitationContent";
                break;
            case "error":
                content["__typename"] = "AgentActivityErrorContent";
                break;
            case "prompt":
                content["__typename"] = "AgentActivityPromptContent";
                break;
            case "response":
                content["__typename"] = "AgentActivityResponseContent";
                break;
            default:
                content["__typename"] = "AgentActivityThoughtContent";
                break;
        }
        return content;
    }
}
