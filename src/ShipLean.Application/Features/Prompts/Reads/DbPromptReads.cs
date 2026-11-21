using Dapper;
using Npgsql;
using ShipLean.Application.Features.Prompts.Sections;
using ShipLean.Application.Features.Prompts.Updates;
using ShipLean.Contracts;
using ShipLean.Contracts.Prompts;
using ShipLean.Core;

namespace ShipLean.Application.Features.Prompts.Reads;

public sealed class DbPromptReads(NpgsqlDataSource dataSource, IBuiltInPrompts builtIns, IViewerZone viewerZone, TimeProvider clock) : IPromptReads
{
    private const string VersionColumns =
        """
        v.prompt, v.id, v.number, v.text, v.base_hash AS BaseHash, b.text AS BaseText, b.first_seen_at AS BaseFirstSeen, v.note, v.source,
        COALESCE(u.name, 'ShipLean') AS "By", v.created_at AS CreatedAt
        """;

    private const string VersionJoins =
        """
        FROM prompt_version v
        JOIN prompt_built_in b ON b.prompt = v.prompt AND b.hash = v.base_hash
        LEFT JOIN app_user u ON u.id = v.created_by
        """;

    public async Task<PromptsPageDto> PageAsync(Guid projectId, ProjectRole role, CancellationToken cancellationToken = default)
    {
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
        var versions = (await connection.QueryAsync<PromptVersionRow>(new CommandDefinition(
            $"SELECT {VersionColumns} {VersionJoins} WHERE v.project_id = @projectId ORDER BY v.prompt, v.number DESC",
            new { projectId }, cancellationToken: cancellationToken))).ToLookup(row => row.Prompt);
        var pointers = (await connection.QueryAsync<PromptPointerRow>(new CommandDefinition(
            "SELECT prompt, in_use_version_id AS InUseVersionId, kept_built_in_hash AS KeptBuiltInHash FROM project_prompt WHERE project_id = @projectId",
            new { projectId }, cancellationToken: cancellationToken))).ToDictionary(row => row.Prompt);
        var current = (await connection.QueryAsync<PromptBuiltInRow>(new CommandDefinition(
            "SELECT prompt, hash, text, first_seen_at AS FirstSeenAt FROM prompt_built_in",
            cancellationToken: cancellationToken))).ToDictionary(row => (row.Prompt, row.Hash));

        var prompts = builtIns.All.ToDictionary(facts => facts.Key, facts =>
        {
            var wire = PromptKeys.ToWire(facts.Key);
            return Prompt(facts, versions[wire].ToList(), pointers.GetValueOrDefault(wire), current);
        });
        var stages = builtIns.Stages
            .Select(stage => new PromptStageDto(stage.Id, stage.Name, stage.When,
                [.. builtIns.All.Where(f => f.Stage == stage.Id).Select(f => prompts[f.Key])]))
            .ToList();
        var customCount = prompts.Values.Count(p => p.Status is PromptStatus.Custom or PromptStatus.BuiltInUpdated);
        return new PromptsPageDto(
            stages, customCount, role == ProjectRole.Admin, admins, [.. PromptAgents.All.Select(id => new PromptAgentDto(id, PromptAgents.Label(id)))], builtIns.BuiltInOnly);
    }

    public async Task<PromptHistoryDto> HistoryAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken = default)
    {
        var rows = await ProjectVersionsAsync(projectId, key, cancellationToken);
        var inUse = rows.InUseId;
        var zone = viewerZone.Zone;
        var versions = rows.Versions
            .Select(v => new PromptVersionDto(v.Number, v.Note, v.By ?? "ShipLean", Label(v.CreatedAt, zone), v.Id == inUse,
                Enum.GetValues<PromptVersionSource>().First(s => PromptKeys.ToWire(s) == v.Source), v.Text))
            .ToList();
        var oldest = rows.Versions.LastOrDefault();
        return new PromptHistoryDto(key, versions, oldest is null ? null : BaseLabel(oldest.BaseFirstSeen, zone));
    }

    public async Task<PromptAssembledDto> AssembledAsync(Guid projectId, PromptKey key, string agent, CancellationToken cancellationToken = default)
    {
        var rows = await ProjectVersionsAsync(projectId, key, cancellationToken);
        var facts = builtIns.Facts(key);
        var inUse = rows.Versions.FirstOrDefault(v => v.Id == rows.InUseId);
        var text = inUse?.Text ?? builtIns.Text(key);
        var label = PromptAgents.Label(agent);
        var untagged = AgentSections.Filter(text, PromptAgents.Other);
        var forAgent = AgentSections.Filter(text, agent);
        var layers = new List<PromptLayerDto>
        {
            new(PromptLayerKind.ServerInstructions, "Server instructions", builtIns.ServerInstructions, false, "Sent when the agent connects. Same for every project."),
            new(PromptLayerKind.Prompt, inUse is null ? $"{facts.Name}, built-in" : $"{facts.Name}, custom v{inUse.Number}", untagged, false, null),
        };
        if (agent != PromptAgents.Other)
        {
            var own = SectionLines(untagged, forAgent);
            layers.Add(new PromptLayerDto(PromptLayerKind.AgentSections, "Agent-specific sections", own, false, SectionNote(text, agent, label)));
        }
        if (key is PromptKey.Work or PromptKey.Guide or PromptKey.Brief or PromptKey.Lead)
        {
            layers.Add(new PromptLayerDto(PromptLayerKind.WaysOfWorking, "Ways of working", null, true, "Added after the prompt once the project has them."));
        }
        layers.Add(new PromptLayerDto(PromptLayerKind.ClientNotes, "Notes for the connecting agent", null, true, "Per-client notes, added last."));
        return new PromptAssembledDto(key, agent, layers, forAgent);
    }

    private static string SectionNote(string text, string agent, string label)
    {
        var count = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Count(line => line.Trim() == $"<{agent}>");
        if (count == 0 || AgentSections.Check(text).Count > 0)
        {
            return $"No sections for {label}";
        }
        var noun = count == 1 ? "section" : "sections";
        return $"{count} {noun} for {label}; the rest are dropped.";
    }

    // The lines the agent gets beyond the untagged text: filtering keeps the untagged lines in order, so what is left over is the agent's sections.
    private static string? SectionLines(string untagged, string forAgent)
    {
        var rest = new List<string>();
        var remaining = untagged.Split('\n').ToList();
        foreach (var line in forAgent.Split('\n'))
        {
            var at = remaining.IndexOf(line);
            if (at == 0)
            {
                remaining.RemoveAt(0);
            }
            else
            {
                rest.Add(line);
            }
        }
        return rest.Count == 0 ? null : string.Join('\n', rest);
    }

    private PromptDto Prompt(PromptFacts facts, List<PromptVersionRow> versions, PromptPointerRow? pointer, Dictionary<(string, string), PromptBuiltInRow> builtInRows)
    {
        var zone = viewerZone.Zone;
        var currentText = builtIns.Text(facts.Key);
        var currentHash = builtIns.Hash(facts.Key);
        var inUse = pointer?.InUseVersionId is { } id ? versions.FirstOrDefault(v => v.Id == id) : null;
        var shown = inUse ?? versions.FirstOrDefault();
        if (shown is null)
        {
            return Build(facts, PromptStatus.BuiltIn, currentText, currentHash, null, null, null, 0);
        }
        var custom = new PromptCustomDto(shown.Number, shown.Text, inUse is not null, shown.By ?? "ShipLean", Label(shown.CreatedAt, zone), shown.Note,
            BaseLabel(shown.BaseFirstSeen, zone), shown.BaseText);
        var patch = PromptDiff.Patch(shown.BaseText, shown.Text);
        var errors = AgentSections.Check(shown.Text).Count;
        if (inUse is null)
        {
            return Build(facts, PromptStatus.UsingBuiltIn, currentText, currentHash, custom, patch, null, errors);
        }
        if (inUse.BaseHash == currentHash || pointer?.KeptBuiltInHash == currentHash)
        {
            return Build(facts, PromptStatus.Custom, currentText, currentHash, custom, patch, null, errors);
        }
        var changedAt = builtInRows[(PromptKeys.ToWire(facts.Key), currentHash)].FirstSeenAt;
        var merge = PromptMerge.Merge(inUse.BaseText, inUse.Text, currentText);
        var update = new PromptUpdateDto($"{Day(changedAt, zone)} built-in", BaseLabel(inUse.BaseFirstSeen, zone),
            PromptDiff.Patch(inUse.BaseText, currentText), merge.Clean, merge.Text);
        return Build(facts, PromptStatus.BuiltInUpdated, currentText, currentHash, custom, patch, update, errors);
    }

    private static PromptDto Build(PromptFacts f, PromptStatus status, string text, string hash, PromptCustomDto? custom, string? patch, PromptUpdateDto? update, int errors)
        => new(f.Key, f.File, f.Name, f.WhenRuns, f.ServedBy, f.ReadBy, f.Note, f.Placeholders, f.InsertedParts, status, text, hash, custom, patch, update, errors);

    private async Task<(List<PromptVersionRow> Versions, Guid? InUseId)> ProjectVersionsAsync(Guid projectId, PromptKey key, CancellationToken cancellationToken)
    {
        var wire = PromptKeys.ToWire(key);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var versions = (await connection.QueryAsync<PromptVersionRow>(new CommandDefinition(
            $"SELECT {VersionColumns} {VersionJoins} WHERE v.project_id = @projectId AND v.prompt = @wire ORDER BY v.number DESC",
            new { projectId, wire }, cancellationToken: cancellationToken))).ToList();
        var inUse = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT in_use_version_id FROM project_prompt WHERE project_id = @projectId AND prompt = @wire",
            new { projectId, wire }, cancellationToken: cancellationToken));
        return (versions, inUse);
    }

    private string Label(DateTime utc, TimeZoneInfo zone) => Day(utc, zone);

    private string Day(DateTime utc, TimeZoneInfo zone) => DisplayDate.ShortDay(new DateTimeOffset(DateTime.SpecifyKind(utc, DateTimeKind.Utc)), clock.GetUtcNow(), zone);

    private string BaseLabel(DateTime firstSeen, TimeZoneInfo zone) => $"built-in of {Day(firstSeen, zone)}";
}
