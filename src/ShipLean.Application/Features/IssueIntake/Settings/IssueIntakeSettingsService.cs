using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Webhooks;
using ShipLean.Application.Integrations;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Users;
using ShipLean.Contracts.WorkItems;
using Microsoft.Extensions.Configuration;

namespace ShipLean.Application.Features.IssueIntake.Settings;

// A project's GitHub issue intake settings (C5). Callers check access first (Member to read, Admin to save).
[RegisterPerRequest]
public sealed class IssueIntakeSettingsService(
    NpgsqlDataSource dataSource, TimeProvider clock, IStateCatalog states, IProjectEvents events, ICurrentUser currentUser, IIntegrationSettings integrations, IConfiguration configuration)
{
    public const string DefaultInstructions = "Write a proposal for this issue to the project's standards.";

    private const int MaxInstructions = 20000;
    private static readonly WorkItemTypeId[] IntakeTypes = [WorkItemTypeId.Story, WorkItemTypeId.Bug, WorkItemTypeId.Feature];

    public async Task<IssueIntakeSettingsDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var row = await connection.QuerySingleOrDefaultAsync<Row>(new CommandDefinition(
            $"""
            SELECT p.repo_url AS repoUrl, (p.agent_connection_id IS NOT NULL AND c.archived_at IS NULL) AS hasConnection,
                   COALESCE(i.enabled, false) AS enabled, i.repository, i.proposal_instructions AS instructions, COALESCE(i.max_rounds, 5) AS maxRounds,
                   {UserRefs.Columns}
            FROM project p
            LEFT JOIN agent_connection c ON c.id = p.agent_connection_id
            LEFT JOIN project_issue_intake i ON i.project_id = p.id
            LEFT JOIN app_user u ON u.id = i.run_as_user_id
            WHERE p.id = @projectId
            """,
            new { projectId }, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("There is no such project.");

        var fromUrl = GitHubRepositoryName.FromRepoUrl(row.RepoUrl);
        var runAs = row.Id is { } id ? new UserRef(id, row.Name!, row.Initials!, row.AvatarColour!, row.PhotoUrl) : null;
        // The same rule as WebhookSecrets.For (another feature, so not called): the endpoint exists only while the secret is non-blank.
        var token = (await integrations.GetAsync(cancellationToken)).GitHubToken;
        return new IssueIntakeSettingsDto(
            row.Enabled,
            row.Enabled ? row.Repository : fromUrl,
            runAs,
            row.Instructions ?? string.Empty,
            DefaultInstructions,
            row.MaxRounds,
            new IssueIntakeReadiness(fromUrl is not null, row.HasConnection, !string.IsNullOrEmpty(token), !string.IsNullOrWhiteSpace(configuration[WebhookSecrets.GithubKey]), runAs is not null));
    }

    // Throws ApiException.NotFound (no such project), Validation (maxRounds under 1, instructions too long), NoRepoUrl
    // (turning on without a github.com repo_url), NotMember (turning on without a run-as member, or one who is not a member)
    // and RepositoryTaken (another project has intake on for that repository). Turning intake on adds the Triage states.
    public async Task<IssueIntakeSettingsDto> SaveAsync(Guid projectId, IssueIntakeSaveDto save, CancellationToken cancellationToken = default)
    {
        if (save.MaxRounds < 1)
        {
            throw ApiException.Validation("The round cap is at least 1.", "maxRounds");
        }

        var instructions = save.ProposalInstructions?.Trim() ?? string.Empty;
        if (instructions.Length > MaxInstructions)
        {
            throw ApiException.Validation($"The proposal instructions are at most {MaxInstructions} characters.", "proposalInstructions");
        }

        var statesAdded = false;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var repoUrl = await connection.QuerySingleOrDefaultAsync<string?>(new CommandDefinition(
                "SELECT COALESCE(repo_url, '') FROM project WHERE id = @projectId FOR NO KEY UPDATE", new { projectId }, transaction, cancellationToken: cancellationToken))
                ?? throw ApiException.NotFound("There is no such project.");
            var repository = save.Enabled
                ? GitHubRepositoryName.FromRepoUrl(repoUrl) ?? throw ApiException.NoRepoUrl("Set the project's repository to a github.com address before turning intake on.")
                : null;
            await CheckRunAsAsync(connection, transaction, projectId, save, cancellationToken);

            try
            {
                await connection.ExecuteAsync(new CommandDefinition(
                    """
                    INSERT INTO project_issue_intake (project_id, enabled, repository, run_as_user_id, proposal_instructions, max_rounds, updated_by, updated_at)
                    VALUES (@projectId, @enabled, @repository, @runAs, @instructions, @maxRounds, @by, @now)
                    ON CONFLICT (project_id) DO UPDATE SET enabled = EXCLUDED.enabled,
                        repository = COALESCE(EXCLUDED.repository, project_issue_intake.repository), run_as_user_id = EXCLUDED.run_as_user_id,
                        proposal_instructions = EXCLUDED.proposal_instructions, max_rounds = EXCLUDED.max_rounds,
                        updated_by = EXCLUDED.updated_by, updated_at = EXCLUDED.updated_at
                    """,
                    new { projectId, enabled = save.Enabled, repository, runAs = save.RunAsUserId, instructions, maxRounds = save.MaxRounds, by = currentUser.Id, now = clock.GetUtcNow() },
                    transaction, cancellationToken: cancellationToken));
            }
            catch (PostgresException taken) when (taken.SqlState == PostgresErrorCodes.UniqueViolation && taken.ConstraintName == "ux_project_issue_intake_repository")
            {
                throw ApiException.RepositoryTaken($"Another project already takes issues from {repository}.");
            }

            if (save.Enabled)
            {
                statesAdded = await AddTriageStatesAsync(connection, transaction, projectId, cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }

        if (statesAdded)
        {
            await events.PublishAsync(new StatesChanged(projectId));
        }

        return await GetAsync(projectId, cancellationToken);
    }

    // Turning on needs a run-as member; a run-as person is checked whenever one is given.
    private static async Task CheckRunAsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, IssueIntakeSaveDto save, CancellationToken cancellationToken)
    {
        if (save.RunAsUserId is not { } runAs)
        {
            if (save.Enabled)
            {
                throw ApiException.NotMember("Choose a project member to run proposals as before turning intake on.");
            }

            return;
        }

        var member = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM project_member WHERE project_id = @projectId AND user_id = @runAs AND archived_at IS NULL)",
            new { projectId, runAs }, transaction, cancellationToken: cancellationToken));
        if (!member)
        {
            throw ApiException.NotMember("The run-as person must be a member of this project.");
        }
    }

    // Triage (proposed, first in the order, intake) for Story, Bug and Feature, where the type has none. Returns whether any was added.
    private async Task<bool> AddTriageStatesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, CancellationToken cancellationToken)
    {
        var have = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT type FROM state WHERE project_id = @projectId AND is_intake AND archived_at IS NULL", new { projectId }, transaction, cancellationToken: cancellationToken)))
            .Select(t => Enum.Parse<WorkItemTypeId>(t, ignoreCase: true)).ToHashSet();
        var missing = IntakeTypes.Where(t => !have.Contains(t)).ToArray();
        if (missing.Length == 0)
        {
            return false;
        }

        // "Triage" unless the type already has a state of that name that is not intake: names are unique per type, and that state stays as it is.
        var taken = (await connection.QueryAsync<string>(new CommandDefinition(
            "SELECT type FROM state WHERE project_id = @projectId AND lower(name) = 'triage' AND archived_at IS NULL", new { projectId }, transaction, cancellationToken: cancellationToken)))
            .Select(t => Enum.Parse<WorkItemTypeId>(t, ignoreCase: true)).ToHashSet();
        var adds = missing.Select(t => new StateAdd(Guid.CreateVersion7(), t, taken.Contains(t) ? "GitHub triage" : "Triage", StateCategory.Proposed, IsIntake: true)).ToArray();
        await states.ApplyAsync(projectId, new StateChanges(adds, [], [.. adds.Select(a => new StateReorder(a.Type, [a.Id]))], []), transaction, cancellationToken);
        return true;
    }

    private sealed record Row(
        string? RepoUrl, bool HasConnection, bool Enabled, string? Repository, string? Instructions, int MaxRounds, Guid? Id, string? Name, string? Initials, string? AvatarColour, string? PhotoUrl);
}
