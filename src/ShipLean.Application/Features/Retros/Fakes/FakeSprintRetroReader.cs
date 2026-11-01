using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Fakes;

// TODO: replaced by the real sprint retro reader, which deletes this file and registers its own module.
internal sealed class FakeSprintRetroReader : ISprintRetroReader
{
    public Task<SprintRetroDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        var point = FakeRetroData.Point();
        var sprint = FakeRetroData.Sprint with { Id = sprintId };
        IReadOnlyList<RetroCategoryColumnDto> columns =
        [
            new(RetroCategory.WentBadly, [new(FakeRetroData.Theme, true, [point with { Category = RetroCategory.WentBadly }])]),
            new(RetroCategory.Improve, [new(FakeRetroData.Theme, true, [point])]),
            new(RetroCategory.NotInPlan, []),
            new(RetroCategory.WentWell, []),
        ];
        var result = new SprintRetroDto(
            sprint, new RetroKpisDto(1, 1, 1, 1), [sprint], columns, [FakeRetroData.Card()],
            [new RetroCoverageRowDto(FakeRetroData.Item, RetroCoverage.Done)],
            [new RetroActionRowDto(FakeRetroData.Action, FakeRetroData.Theme, sprint, null, null)],
            [new RetroMentionStripDto(FakeRetroData.Theme, [1, 2, 3], null)]);
        return Task.FromResult(result);
    }
}

public sealed class FakeSprintRetroReaderServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<ISprintRetroReader, FakeSprintRetroReader>();
}
