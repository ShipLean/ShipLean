using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Prompts;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Prompts;

// A project's built-in prompts and its custom versions of them (SHIP-744 C1). Reads need Member, writes Admin; each action checks access first,
// then the prompt key from the route. The services trust their caller.
[Route("api/projects/{id:guid}/prompts")]
public sealed class ProjectPromptsController(
    IProjectAccess access, IPromptReads reads, IPromptWrites writes, ICurrentUser currentUser, IUsageEvents events, IUsageScope scope, TimeProvider time) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<PromptsPageDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<PromptsPageDto> Page(Guid id, CancellationToken cancellationToken)
        => await reads.PageAsync(id, await access.RequireAsync(id, ProjectRole.Member, cancellationToken), cancellationToken);

    [HttpGet("{prompt}/history")]
    [ProducesResponseType<PromptHistoryDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<PromptHistoryDto> History(Guid id, string prompt, CancellationToken cancellationToken)
        => await reads.HistoryAsync(id, await KeyAsync(id, prompt, ProjectRole.Member, cancellationToken), cancellationToken);

    [HttpGet("{prompt}/assembled")]
    [ProducesResponseType<PromptAssembledDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<PromptAssembledDto> Assembled(Guid id, string prompt, [FromQuery] string? agent, CancellationToken cancellationToken)
    {
        var key = await KeyAsync(id, prompt, ProjectRole.Member, cancellationToken);
        if (agent is null || !PromptAgents.All.Contains(agent))
        {
            throw ApiException.Validation($"Agent must be one of {string.Join(", ", PromptAgents.All)}.", "agent");
        }
        return await reads.AssembledAsync(id, key, agent, cancellationToken);
    }

    [HttpPost("{prompt}/versions")]
    [ProducesResponseType<PromptSavedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<PromptSavedDto> SaveVersion(Guid id, string prompt, PromptSaveDto save, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var key = await KeyAsync(id, prompt, ProjectRole.Admin, cancellationToken);
        var saved = await writes.SaveAsync(id, key, save, cancellationToken: cancellationToken);
        var name = save.Source == PromptVersionSource.BuiltInUpdate ? UsageEventNames.PromptUpdateApplied : UsageEventNames.PromptSaved;
        SelfEmittedUsage.TrackOk(
            events, scope, time, started, name, currentUser.Id, "api:ProjectPrompts.SaveVersion", id,
            new Dictionary<string, object> { [UsageProps.Prompt] = PromptKeys.ToWire(key) });
        return saved;
    }

    [HttpPost("{prompt}/use-built-in")]
    [ProducesResponseType<PromptSavedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<PromptSavedDto> UseBuiltIn(Guid id, string prompt, CancellationToken cancellationToken)
        => await writes.UseBuiltInAsync(id, await TrackedKeyAsync(id, prompt, cancellationToken), cancellationToken);

    [HttpPost("{prompt}/versions/{number:int}/use")]
    [ProducesResponseType<PromptSavedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<PromptSavedDto> UseVersion(Guid id, string prompt, int number, CancellationToken cancellationToken)
        => await writes.UseVersionAsync(id, await TrackedKeyAsync(id, prompt, cancellationToken), number, cancellationToken);

    [HttpPost("{prompt}/update/keep")]
    [ProducesResponseType<PromptSavedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public async Task<PromptSavedDto> KeepMine(Guid id, string prompt, CancellationToken cancellationToken)
        => await writes.KeepMineAsync(id, await TrackedKeyAsync(id, prompt, cancellationToken), cancellationToken);

    // Access first, then the key: a non-member never learns which keys exist.
    private async Task<PromptKey> KeyAsync(Guid id, string prompt, ProjectRole minimum, CancellationToken cancellationToken)
    {
        await access.RequireAsync(id, minimum, cancellationToken);
        return PromptKeys.TryParse(prompt, out var key) ? key : throw ApiException.NotFound("There is no such prompt.");
    }

    // An admin write whose event the action filter records: the prompt goes on it.
    private async Task<PromptKey> TrackedKeyAsync(Guid id, string prompt, CancellationToken cancellationToken)
    {
        var key = await KeyAsync(id, prompt, ProjectRole.Admin, cancellationToken);
        scope.AddProperty(UsageProps.Prompt, PromptKeys.ToWire(key));
        return key;
    }
}
