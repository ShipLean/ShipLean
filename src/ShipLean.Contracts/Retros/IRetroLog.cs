namespace ShipLean.Contracts.Retros;

// What the MCP tools log_retrospective and list_retrospectives do. Callers have resolved the item and project and checked access.
public interface IRetroLog
{
    Task<RetroLogResult> LogAsync(RetroLogRequest request, CancellationToken cancellationToken = default);

    Task<RetroListResult> ListAsync(RetroListRequest request, CancellationToken cancellationToken = default);
}
