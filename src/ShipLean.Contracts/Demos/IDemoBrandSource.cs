using System.Data.Common;

namespace ShipLean.Contracts.Demos;

public interface IDemoBrandSource
{
    Task<DemoBrandInspection> InspectAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<DemoBrand> PrepareAsync(
        DemoBrandInspection inspection,
        Guid projectId,
        Guid itemId,
        Guid userId,
        Guid? authorizationId,
        string agentStamp,
        string baseUrl,
        DbTransaction transaction,
        CancellationToken cancellationToken = default);
}
