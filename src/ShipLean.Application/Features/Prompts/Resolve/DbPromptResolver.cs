using Dapper;
using Npgsql;
using ShipLean.Application.Features.Prompts.Sections;
using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Resolve;

public sealed class DbPromptResolver(
    NpgsqlDataSource dataSource,
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
        var hash = builtIns.Hash(request.Key);

        if (request.ProjectId is not { } projectId)
        {
            return new ResolvedPrompt(request.Key, AgentSections.Filter(builtIns.Text(request.Key), agent), null, agent, hash);
        }

        // Read on every call, never cached: a save reaches the next call.
        InUse? inUse;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        {
            inUse = await connection.QuerySingleOrDefaultAsync<InUse>(new CommandDefinition(
                """
                SELECT pv.number AS Number, pv.text AS Text
                FROM project_prompt pp
                JOIN prompt_version pv ON pv.id = pp.in_use_version_id AND pv.project_id = pp.project_id AND pv.prompt = pp.prompt
                WHERE pp.project_id = @projectId AND pp.prompt = @prompt
                """,
                new { projectId, prompt = PromptKeys.ToWire(request.Key) }, cancellationToken: cancellationToken));
        }

        var text = AgentSections.Filter(inUse?.Text ?? builtIns.Text(request.Key), agent);
        await servingLog.RecordAsync(
            new PromptServing(projectId, request.Key, inUse?.Number, hash, agent, connected?.Raw, connected is null ? null : CamelCase(connected.Source), request.ItemKey, userId, time.GetUtcNow()),
            cancellationToken);
        return new ResolvedPrompt(request.Key, text, inUse?.Number, agent, hash);
    }

    private static string CamelCase(AgentSignalSource source) => char.ToLowerInvariant(source.ToString()[0]) + source.ToString()[1..];

    private sealed record InUse(int Number, string Text);
}
