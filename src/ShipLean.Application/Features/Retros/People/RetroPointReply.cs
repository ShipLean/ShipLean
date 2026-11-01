using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// The point as the UI shows it, read back through the cards after the write committed.
internal static class RetroPointReply
{
    public static async Task<RetroPointDto> LoadAsync(IRetroCards cards, Guid retroId, Guid pointId, CancellationToken cancellationToken)
    {
        var loaded = await cards.LoadAsync([retroId], cancellationToken);
        return loaded.SelectMany(card => card.Points).FirstOrDefault(point => point.Id == pointId)
            ?? throw ApiException.NotFound("No retro point with that id was found.");
    }
}
