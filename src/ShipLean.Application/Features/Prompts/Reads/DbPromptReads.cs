using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;

namespace ShipLean.Application.Features.Prompts.Reads;

public sealed class DbPromptReads(NpgsqlDataSource dataSource, IBuiltInPrompts builtIns) : IPromptReads
{
    public async Task<PromptsPageDto> PageAsync(Guid projectId, ProjectRole role, CancellationToken cancellationToken = default)
    {
        // TODO: the real page read, with each project's versions, status and update
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var admins = (await connection.QueryAsync<string>(new CommandDefinition(
            """
            SELECT u.name
            FROM project_member m
            JOIN app_user u ON u.id = m.user_id
            WHERE m.project_id = @projectId AND m.role = 'admin' AND m.archived_at IS NULL
            ORDER BY u.name
            """,
            new { projectId }, cancellationToken: cancellationToken))).ToList();
        var stages = builtIns.Stages
            .Select(stage => new PromptStageDto(stage.Id, stage.Name, stage.When,
                [.. builtIns.All.Where(f => f.Stage == stage.Id).Select(BuiltInOnly)]))
            .ToList();
        return new PromptsPageDto(
            stages, 0, role == ProjectRole.Admin, admins, [.. PromptAgents.All.Select(id => new PromptAgentDto(id, PromptAgents.Label(id)))], builtIns.BuiltInOnly);
    }

    public Task<PromptHistoryDto> HistoryAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default)
        => Task.FromResult(new PromptHistoryDto(key, [], null)); // TODO: the real history read

    public Task<PromptAssembledDto> AssembledAsync(Guid projectId, PromptKey key, string agent, CancellationToken cancellationToken = default)
    {
        // TODO: the real assembly: the version in use, the agent's sections and the project's layers
        var text = builtIns.Text(key);
        PromptLayerDto[] layers =
        [
            new(PromptLayerKind.ServerInstructions, "Server instructions", builtIns.ServerInstructions, false, null),
            new(PromptLayerKind.Prompt, "Prompt", text, false, null),
            new(PromptLayerKind.AgentSections, "Agent sections", null, false, null),
            new(PromptLayerKind.WaysOfWorking, "Ways of working", null, true, "Not built yet."),
            new(PromptLayerKind.ClientNotes, "Client notes", null, true, "Not built yet."),
        ];
        return Task.FromResult(new PromptAssembledDto(key, agent, layers, text));
    }

    private PromptDto BuiltInOnly(PromptFacts f)
        => new(f.Key, f.File, f.Name, f.WhenRuns, f.ServedBy, f.ReadBy, f.Note, f.Placeholders, f.InsertedParts, PromptStatus.BuiltIn,
            builtIns.Text(f.Key), builtIns.Hash(f.Key), null, null, null, 0);
}
