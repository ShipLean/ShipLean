namespace ShipLean.Contracts.Retros;

// The themes a project has points under, for the theme pickers.
public interface IRetroThemeReader
{
    Task<RetroThemeListDto> ListAsync(Guid projectId, CancellationToken cancellationToken = default);
}
