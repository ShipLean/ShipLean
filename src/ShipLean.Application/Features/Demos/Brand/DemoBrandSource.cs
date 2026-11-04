using System.Data.Common;
using ShipLean.Contracts.Demos;

namespace ShipLean.Application.Features.Demos.Brand;

// TODO: the real source replaces this: it reads the project's logo and tokens and issues the logo link. With no brand set, ShipLean's defaults are final.
public sealed class DemoBrandSource : IDemoBrandSource
{
    public Task<DemoBrandInspection> InspectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(new DemoBrandInspection(null, null, null, null));

    public Task<DemoBrand> PrepareAsync(
        DemoBrandInspection inspection,
        Guid projectId,
        Guid itemId,
        Guid userId,
        Guid? authorizationId,
        string agentStamp,
        string baseUrl,
        DbTransaction transaction,
        CancellationToken cancellationToken = default)
        => Task.FromResult(DemoBrandDefaults.Brand);
}
