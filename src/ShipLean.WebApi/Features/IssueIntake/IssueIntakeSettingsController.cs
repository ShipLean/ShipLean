using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.IssueIntake;
using ShipLean.Contracts.Refs;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.IssueIntake;

// A project's GitHub issue intake (C5): members read it, admins change it.
[Route("api/projects/{id:guid}/settings/issue-intake")]
public sealed class IssueIntakeSettingsController(IProjectAccess access) : ApiControllerBase
{
    // TODO: the stored settings and readiness, and a save that checks the repository, the run-as member and the
    // Triage states, replace this sample. It equals DtoFixtures/issueIntake/issue-intake-settings.json.
    private static readonly IssueIntakeSettingsDto Sample = new(
        Enabled: true,
        Repository: "ShipLean/ShipLean",
        RunAs: new UserRef(Guid.Parse("00000001-0000-4000-8000-000000000002"), "Priya Shah", "PS", "#2f7fd8", null),
        ProposalInstructions: "",
        DefaultProposalInstructions: "Write a proposal for this issue to the project's standards.",
        MaxRounds: 5,
        Readiness: new IssueIntakeReadiness(HasGitHubRepository: true, HasAgentConnection: true, HasGitHubToken: true, HasWebhookSecret: false, HasRunAs: true));

    [HttpGet]
    [ProducesResponseType<IssueIntakeSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IssueIntakeSettingsDto> Get(Guid id, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
        return Sample;
    }

    [HttpPut]
    [ProducesResponseType<IssueIntakeSettingsDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<IssueIntakeSettingsDto> Put(Guid id, IssueIntakeSaveDto save, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, ProjectRole.Admin, cancellationToken);
        return Sample;
    }
}
