namespace ShipLean.Contracts.Retros;

// What the action dialog offers (GET api/projects/{id}/retros/action-dialog). pointIds are the selected points, themeId selects that theme's
// undecided points, itemId and sprintId scope what is offered.
public interface IRetroActionDialogReader
{
    Task<RetroActionDialogDto> GetAsync(Guid projectId, IReadOnlyList<Guid> pointIds, Guid? themeId, Guid? itemId, Guid? sprintId, CancellationToken cancellationToken = default);
}
