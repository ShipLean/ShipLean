using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.ActionDialog;

// Nothing selected, nothing offered, no open actions, no suggested theme.
// TODO: the real dialog read (selected points, offered undecided points, open actions, suggested theme) replaces this.
public sealed class RetroActionDialogReader : IRetroActionDialogReader
{
    public Task<RetroActionDialogDto> GetAsync(Guid projectId, IReadOnlyList<Guid> pointIds, Guid? themeId, Guid? itemId, Guid? sprintId, CancellationToken cancellationToken = default)
        => Task.FromResult(new RetroActionDialogDto([], [], [], null));
}
