using ShipLean.Application.Features.LinearApi;

namespace ShipLean.WebApi.Features.LinearApi;

internal static class LinearPersonMap
{
    // A ShipLean person as Linear's User. The email is not shared with an agent.
    public static IReadOnlyDictionary<string, object?> Of(LinearPerson person) => new Dictionary<string, object?>
    {
        ["id"] = person.Id.ToString(),
        ["name"] = person.Name,
        ["displayName"] = person.Name,
        ["active"] = true,
    };

    // The calling connection as Linear's app user (viewer, AgentSession.appUser, an activity's user).
    public static IReadOnlyDictionary<string, object?> App(LinearCaller caller) => new Dictionary<string, object?>
    {
        ["id"] = caller.ConnectionId.ToString(),
        ["name"] = caller.Name,
        ["displayName"] = caller.Name,
        ["active"] = true,
        ["app"] = true,
    };
}
