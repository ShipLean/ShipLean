using System.Data.Common;
using ShipLean.Contracts.Demos;

namespace ShipLean.WebApi.Tests.Demos;

// A brand source that returns one given brand, so a test can tell a brand passed on from a default.
internal sealed class FixedBrandSource(DemoBrand brand) : IDemoBrandSource
{
    public Task<DemoBrandInspection> InspectAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(new DemoBrandInspection(null, null, null, null));

    public Task<DemoBrand> PrepareAsync(
        DemoBrandInspection inspection, Guid projectId, Guid itemId, Guid userId, Guid? authorizationId, string agentStamp, string baseUrl, DbTransaction transaction, CancellationToken cancellationToken = default)
        => Task.FromResult(brand);
}
