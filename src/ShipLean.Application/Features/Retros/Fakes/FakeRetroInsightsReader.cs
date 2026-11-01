using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Fakes;

// TODO: replaced by the real insights reader, which deletes this file and registers its own module.
internal sealed class FakeRetroInsightsReader : IRetroInsightsReader
{
    public Task<RetroInsightsDto> GetAsync(Guid projectId, RetroRange range, CancellationToken cancellationToken = default)
    {
        var theme = new RetroThemeInsightDto(FakeRetroData.Theme, [1, 2, 3], 6, RetroThemeStatus.Recurring, [FakeRetroData.Action], null);
        var impact = new RetroImpactDto(FakeRetroData.Action, FakeRetroData.Theme, RetroVerdict.TooEarly, [1, 2, 3], 1, 2d, null, null, FakeRetroData.Point());
        var result = new RetroInsightsDto(
            range.ToString(), [FakeRetroData.Sprint], [theme], [],
            [new RetroActionRowDto(FakeRetroData.Action, FakeRetroData.Theme, FakeRetroData.Sprint, null, null)], [impact]);
        return Task.FromResult(result);
    }
}

public sealed class FakeRetroInsightsReaderServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IRetroInsightsReader, FakeRetroInsightsReader>();
}
