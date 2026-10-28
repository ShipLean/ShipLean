using HotChocolate;
using HotChocolate.Resolvers;
using ShipLean.Application.Features.LinearApi;

namespace ShipLean.WebApi.Features.LinearApi;

// The Query fields. Every read goes through LinearReads with the calling connection's id, so a session or item that is not
// that connection's is NOT_FOUND, never a partial answer.
internal static class LinearQueries
{
    public static ValueTask<object?> Viewer(IResolverContext context)
        => ValueTask.FromResult<object?>(LinearPersonMap.App(LinearRequest.Of(context).Caller));

    public static async ValueTask<object?> Issue(IResolverContext context)
    {
        var request = LinearRequest.Of(context);
        var issue = await request.Reads.FindIssueAsync(request.Caller.ConnectionId, context.ArgumentValue<string>("id"), context.RequestAborted)
            ?? throw LinearErrors.NotFound("Issue");
        return LinearIssueMap.Of(issue, await request.BaseUrlAsync(context.RequestAborted), request);
    }

    public static async ValueTask<object?> AgentSession(IResolverContext context)
    {
        var request = LinearRequest.Of(context);
        var session = Guid.TryParse(context.ArgumentValue<string>("id"), out var id)
            ? await request.Reads.FindSessionAsync(request.Caller.ConnectionId, id, context.RequestAborted)
            : null;
        return session is null
            ? throw LinearErrors.NotFound("AgentSession")
            : LinearSessionMap.Of(session, await request.BaseUrlAsync(context.RequestAborted), request);
    }

    public static async ValueTask<object?> AgentActivity(IResolverContext context)
    {
        var request = LinearRequest.Of(context);
        var activity = Guid.TryParse(context.ArgumentValue<string>("id"), out var id)
            ? await request.Reads.FindActivityAsync(request.Caller.ConnectionId, id, context.RequestAborted)
            : null;
        return activity is null ? throw LinearErrors.NotFound("AgentActivity") : LinearActivityMap.Of(activity, request.Caller);
    }

    // The session's repository, when it is one of the candidates (hostname and owner/name, compared without case). The
    // suggestion is the candidate as the agent spelled it, with confidence 1; no match is an empty list.
    public static async ValueTask<object?> IssueRepositorySuggestions(IResolverContext context)
    {
        var request = LinearRequest.Of(context);
        var sessionArgument = context.ArgumentValue<string?>("agentSessionId");
        Guid? sessionId = null;
        if (!string.IsNullOrEmpty(sessionArgument))
        {
            sessionId = Guid.TryParse(sessionArgument, out var parsed) ? parsed : throw LinearErrors.NotFound("AgentSession");
        }

        var lookup = await request.Reads.RepositoryAsync(request.Caller.ConnectionId, context.ArgumentValue<string>("issueId"), sessionId, context.RequestAborted);
        if (!lookup.Found)
        {
            throw LinearErrors.NotFound(sessionId is null ? "Issue" : "AgentSession");
        }

        var suggestions = new List<Dictionary<string, object?>>();
        if (lookup.Repository is { } repository)
        {
            foreach (var candidate in Candidates(context).Where(c => repository.Matches(c.Hostname, c.RepositoryFullName)).Take(1))
            {
                suggestions.Add(new Dictionary<string, object?>
                {
                    ["repositoryFullName"] = candidate.RepositoryFullName,
                    ["hostname"] = candidate.Hostname,
                    ["confidence"] = 1.0,
                });
            }
        }
        return new Dictionary<string, object?> { ["suggestions"] = suggestions };
    }

    private static IEnumerable<(string? Hostname, string? RepositoryFullName)> Candidates(IResolverContext context)
    {
        if (context.ArgumentValue<object?>("candidateRepositories") is not IEnumerable<object?> items)
        {
            yield break;
        }
        foreach (var item in items)
        {
            if (item is IReadOnlyDictionary<string, object?> fields)
            {
                yield return (fields.GetValueOrDefault("hostname") as string, fields.GetValueOrDefault("repositoryFullName") as string);
            }
        }
    }
}
