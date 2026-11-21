using ShipLean.Application.Features.Prompts.Sections;
using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Resolve;

public sealed class DbPromptResolver(
    IBuiltInPrompts builtIns,
    ICurrentRequest currentRequest,
    IConnectedAgent connectedAgent,
    ICurrentUser currentUser,
    IPromptServingLog servingLog,
    TimeProvider time) : IPromptResolver
{
    public async Task<ResolvedPrompt> ResolveAsync(PromptRequest request, CancellationToken cancellationToken = default)
    {
        // Off a request there is no current user (reading it throws) and the connected agent is "other".
        var connected = request.Agent is null ? connectedAgent.Current : null;
        var agent = request.Agent ?? connected!.Id;
        var userId = request.UserId ?? (currentRequest.Exists ? currentUser.Id : (Guid?)null);
        // TODO: replaced by the version in use
        var text = AgentSections.Filter(builtIns.Text(request.Key), agent);
        var hash = builtIns.Hash(request.Key);

        if (request.ProjectId is { } projectId)
        {
            await servingLog.RecordAsync(
                new PromptServing(projectId, request.Key, null, hash, agent, connected?.Raw, connected is null ? null : CamelCase(connected.Source), request.ItemKey, userId, time.GetUtcNow()),
                cancellationToken);
        }
        return new ResolvedPrompt(request.Key, text, null, agent, hash);
    }

    private static string CamelCase(AgentSignalSource source) => char.ToLowerInvariant(source.ToString()[0]) + source.ToString()[1..];
}
