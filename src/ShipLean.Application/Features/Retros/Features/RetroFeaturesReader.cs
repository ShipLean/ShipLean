using ShipLean.Application.Features.Retros.Reads;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Features;

// A fixed answer in the final shape: one Feature row and one loose story, whatever the project holds.
// TODO: the real feature rollup replaces this.
public sealed class RetroFeaturesReader : IRetroFeaturesReader
{
    private static readonly StateRef InProgress = new(new Guid("0a2e0000-0000-7000-8000-0000000000f1"), "In progress", StateCategory.InProgress);

    public Task<RetroFeaturesDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(new RetroFeaturesDto(
            [Row(new Guid("0a2e0000-0000-7000-8000-0000000000f2"), "FAKE-1", WorkItemTypeId.Feature, "A feature", noFeatureRetro: false)],
            [Row(new Guid("0a2e0000-0000-7000-8000-0000000000f3"), "FAKE-2", WorkItemTypeId.Story, "A story with no feature", noFeatureRetro: true)]));

    private static RetroFeatureRowDto Row(Guid id, string key, WorkItemTypeId type, string title, bool noFeatureRetro)
        => new(new RetroItemRefDto(id, key, new TypeRef(type, WorkItemTypeRules.Name(type)), title), InProgress, 1, 1, RetroAggregates.Of([]), "1 Nov", noFeatureRetro);
}
